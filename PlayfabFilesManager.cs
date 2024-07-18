#if !DISABLE_PLAYFABENTITY_API && !DISABLE_PLAYFABCLIENT_API
using PlayFab;
using PlayFab.Internal;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class FileModel
{
    public string fileName;
    public string filePath;
}

public class PlayfabFilesManager : MonoBehaviour
{
    public static PlayfabFilesManager Instance;

    [Space]
    [SerializeField] private string defaultFileStoragePath;

    private Dictionary<string, string> filesData = new Dictionary<string, string>();
    private List<FileModel> filesToUpload = new List<FileModel>();

    private string activeUploadFileName;
    private string filePath;
    private int globalFileLock = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        Invoke(nameof(CheckProgressFilesDownload), 2);
    }

    private void CheckProgressFilesDownload()
    {
        if (PlayerPrefs.GetInt(Gods.PROGRESS_FILES_LOADED, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.PROGRESS_FILES_LOADED, 1);

            LoadAllFiles();
        }
    }

    public void UploadFileToPlayfab(string filePath, string fileName)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            this.filePath = filePath;
            GetUploadFileDetails(fileName, filePath);
        }
    }

    public string GetFileData(string fileName)
    {
        string fileData = string.Empty;

        if (filesData.Count > 0)
        {
            foreach (var item in filesData)
            {
                if (item.Key.Equals(fileName))
                {
                    fileData = item.Value;

                    break;
                }
            }
        }

        return fileData;
    }

    #region FILE UPLOAD
    //get upload file details
    void GetUploadFileDetails(string fileName, string filePath)
    {
        if (globalFileLock != 0)
        {
            FileModel file = new FileModel()
            {
                fileName = fileName,
                filePath = filePath
            };

            bool contains = false;
            foreach (var item in filesToUpload)
            {
                if (fileName.Equals(item.fileName))
                {
                    contains = true;

                    break;
                }
            }
            if (!contains) filesToUpload.Add(file);
            //if (!filesToUpload.Contains(file))
            //{
            //    filesToUpload.Add(file);
            //}

            throw new Exception("This example overly restricts file operations for safety. Careful consideration must be made when doing multiple file operations in parallel to avoid conflict.");
        }

        activeUploadFileName = fileName;

        globalFileLock += 1;
        var request = new PlayFab.DataModels.InitiateFileUploadsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey
            {
                Id = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty),
                Type = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty)
            },
            FileNames = new List<string> { activeUploadFileName },
        };
        PlayFabDataAPI.InitiateFileUploads(request, OnInitFileUploadSuccess, OnInitFileUploadFailed);
    }

    private void OnInitFileUploadSuccess(PlayFab.DataModels.InitiateFileUploadsResponse response)
    {
        var payload = GetByteData(filePath);

        globalFileLock += 1;
        PlayFabHttp.SimplePutCall(response.UploadDetails[0].UploadUrl,
            payload,
            FinalizeUpload,
            error => { Debug.LogError(error); }
        );
        globalFileLock -= 1;
    }

    private void OnInitFileUploadFailed(PlayFabError error)
    {
        if (error.Error == PlayFabErrorCode.EntityFileOperationPending)
        {
            globalFileLock += 1;
            var request = new PlayFab.DataModels.AbortFileUploadsRequest
            {
                Entity = new PlayFab.DataModels.EntityKey
                {
                    Id = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty),
                    Type = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty)
                },
                FileNames = new List<string> { activeUploadFileName },
            };
            PlayFabDataAPI.AbortFileUploads(request, (result) =>
            {
                globalFileLock -= 1;
                GetUploadFileDetails(activeUploadFileName, filePath);
            },
            OnSharedFailure);
            globalFileLock -= 2;
        }
        else
            OnSharedFailure(error);
    }

    //upload file from streaming assets
    private void FinalizeUpload(byte[] data)
    {
        globalFileLock += 1;
        var request = new PlayFab.DataModels.FinalizeFileUploadsRequest
        {
            Entity = new PlayFab.DataModels.EntityKey
            {
                Id = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty),
                Type = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty)
            },
            FileNames = new List<string> { activeUploadFileName },
        };
        PlayFabDataAPI.FinalizeFileUploads(request, OnUploadSuccess, OnSharedFailure);
        globalFileLock -= 1;
    }

    private void OnUploadSuccess(PlayFab.DataModels.FinalizeFileUploadsResponse result)
    {
        Debug.LogError("File upload success: " + activeUploadFileName);
        globalFileLock -= 1; // Finish FinalizeFileUploads

        if (filesToUpload.Count > 0)
        {
            FileModel file = filesToUpload[0];
            filesToUpload.RemoveAt(0);

            UploadFileToPlayfab(file.filePath, file.fileName);
        }
    }

    private byte[] GetByteData(string filePath)
    {
        string path = Gods.GetFilePath(filePath);

        if (File.Exists(path))
        {
            string stringData = File.ReadAllText(path);
            return Encoding.UTF8.GetBytes(stringData);
        }
        else
        {
            return null;
        }
    }
    #endregion

    #region FILE FETCH
    public void LoadAllFiles()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            try
            {
                if (globalFileLock != 0)
                    throw new Exception("This example overly restricts file operations for safety. Careful consideration must be made when doing multiple file operations in parallel to avoid conflict.");
            }
            catch
            {
                Invoke(nameof(LoadAllFiles), 2);
            }

            filesData = new Dictionary<string, string>();
            globalFileLock += 1;
            var request = new PlayFab.DataModels.GetFilesRequest
            {
                Entity = new PlayFab.DataModels.EntityKey
                {
                    Id = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty),
                    Type = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty)
                }
            };
            PlayFabDataAPI.GetFiles(request, OnGetFileMeta, OnSharedFailure);
        }
    }

    private void OnGetFileMeta(PlayFab.DataModels.GetFilesResponse result)
    {
        //Debug.LogError("Loading " + result.Metadata.Count + " files");

        filesData.Clear();
        foreach (var eachFilePair in result.Metadata)
        {
            filesData.Add(eachFilePair.Key, null);
            GetActualFile(eachFilePair.Value);
        }
        globalFileLock -= 1;
    }

    private void GetActualFile(PlayFab.DataModels.GetFileMetadata fileData)
    {
        globalFileLock += 1;
        PlayFabHttp.SimpleGetCall(fileData.DownloadUrl,
            result =>
            {
                //Debug.LogError("filename: " + fileData.FileName + ", file" + Encoding.UTF8.GetString(result));
                filesData[fileData.FileName] = Encoding.UTF8.GetString(result);
                SaveJsonToFile(Encoding.UTF8.GetString(result), fileData.FileName);
                globalFileLock -= 1;
            },
            error => { Debug.Log(error); }
        );
    }
    #endregion

    private void OnSharedFailure(PlayFabError error)
    {
        Debug.LogError(error.GenerateErrorReport());
        globalFileLock -= 1;
    }

    private void SaveJsonToFile(string data, string fileName)
    {
        string savePath = Gods.GetFilePath(fileName);

        if (File.Exists(savePath))
        {
            File.Delete(savePath);
        }

        File.WriteAllText(savePath, data);

        StartCoroutine(ICallback(fileName));
    }

    private IEnumerator ICallback(string fileName)
    {
        yield return new WaitForSeconds(1f);

        if (fileName.ToLower().Contains("achievement") && PlayerPrefs.GetInt(Gods.ACHIEVEMENTS_PROGRESS_DOWNLOADED, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.ACHIEVEMENTS_PROGRESS_DOWNLOADED, 1);

            AchievementsProgressManager.Instance.StartLoadingConfig();
        }
        else if (fileName.ToLower().Contains("fort") && PlayerPrefs.GetInt(Gods.FORT_PROGRESS_DOWNLOADED, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.FORT_PROGRESS_DOWNLOADED, 1);

            FortProgressManager.Instance.StartLoadingConfig();
        }
        else if (fileName.ToLower().Contains("quest") && PlayerPrefs.GetInt(Gods.QUEST_PROGRESS_DOWNLOADED, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.QUEST_PROGRESS_DOWNLOADED, 1);

            QuestProgressManager.Instance.StartLoadingConfig();
        }
        else if (fileName.ToLower().Contains("warrior") && PlayerPrefs.GetInt(Gods.WARRIORS_PROGRESS_DOWNLOADED, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.WARRIORS_PROGRESS_DOWNLOADED, 1);

            WarriorsProgressManager.Instance.StartLoadingConfig();
        }
    }
}
#endif