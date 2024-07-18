using PlayFab;
using PlayFab.DataModels;
using PlayFab.Json;
using PlayFab.ServerModels;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayfabDataManager : MonoBehaviour
{
    public static PlayfabDataManager Instance;

    [SerializeField] private GameObject loading;

    [HideInInspector] public Dictionary<string, string> economyData;

    private Coroutine saveCoroutine;

    private int maxSaveTries = 3;
    private int currSaveTry = 0;
    private int maxFetchTries = 3;
    private int currFetchTry = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        FetchEconomyData();
    }

    public void SaveDataToPlayfab()
    {
        if (saveCoroutine != null)
        {
            StopCoroutine(saveCoroutine);
        }
        saveCoroutine = StartCoroutine(ISaveToPlayfab());
    }

    private IEnumerator ISaveToPlayfab()
    {
        yield return new WaitForSeconds(1.5f);

        SaveEconomyData();
    }

    #region SAVE DATA
    private void SaveEconomyData()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable && PlayfabLoginManager.Instance.IsPlayfabLoggedIn())
        {
            var data = new Dictionary<string, object>()
            {
                {"Energy", PlayerPrefs.GetInt(Gods.ENERGY_AMOUNT, 0)},
                {"Coins", PlayerPrefs.GetInt(Gods.COINS_AMOUNT, 0)},
                {"Gems", PlayerPrefs.GetInt(Gods.GEMS_AMOUNT, 0)},
                {"Bricks", PlayerPrefs.GetInt(Gods.BRICKS_AMOUNT, 0) },
                {"Cakes", PlayerPrefs.GetInt(Gods.CAKES_AMOUNT, 0) },
            };
            var dataList = new List<SetObject>()
            {
                new SetObject()
                {
                    ObjectName = "EconomyValues",
                    DataObject = data
                },
            };

            PlayFabDataAPI.SetObjects(new SetObjectsRequest()
            {
                Entity = new PlayFab.DataModels.EntityKey
                {
                    Id = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty),
                    Type = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty)
                },
                Objects = dataList,
            }, (setResult) =>
            {
                //Debug.LogError("data saving success: " + setResult.ProfileVersion);

                PlayerPrefs.SetInt(Gods.SYNC_ECONOMY, 0);
                PlayerPrefs.SetInt(Gods.SYNCING, 0);
                currSaveTry = 0;
            }, OnEconomyDataSaveError);
        }
    }

    public void OnEconomyDataSaveError(PlayFabError error)
    {
        Debug.LogError("error in data saving: " + error.ErrorMessage);

        if (currSaveTry++ < maxSaveTries)
        {
            SaveEconomyData();
        }
        else
        {
            currSaveTry = 0;
        }
    }
    #endregion

    #region FETCH DATA
    public void FetchEconomyData()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable && PlayfabLoginManager.Instance.IsPlayfabLoggedIn())
        {
            economyData = new Dictionary<string, string>();

            var getRequest = new GetObjectsRequest
            {
                Entity = new PlayFab.DataModels.EntityKey
                {
                    Id = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty),
                    Type = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty)
                }
            };

            if (PlayerPrefs.GetInt(Gods.SYNC_ECONOMY, 1) == 0 || PlayerPrefs.GetInt(Gods.SYNCING, 0) == 1)
            {
                PlayFabDataAPI.GetObjects(getRequest,
                    result =>
                    {
                        PlayerPrefs.SetInt(Gods.SYNC_ECONOMY, 0);
                        PlayerPrefs.SetInt(Gods.SYNCING, 0);
                        currFetchTry = 0;

                        var objs = result.Objects;

                        foreach (var obj in objs)
                        {
                            JsonObject jsonResult = (JsonObject)result.Objects[obj.Key].DataObject;
                            foreach (var key in jsonResult.Keys)
                            {
                                object val;
                                jsonResult.TryGetValue(key, out val);
                                if (!economyData.ContainsKey(key))
                                {
                                    economyData.Add(key, val.ToString());

                                    if (key.ToLower().Equals("energy"))
                                    {
                                        int value = Convert.ToInt32(val) < 0 ? 0 : Convert.ToInt32(val);

                                        if (PlayerPrefs.GetInt(Gods.REFILL_ENERGY_ON_START, 0) == 1)
                                        {
                                            EnergyManager.Instance.SetEnergy(value);
                                        }
                                    }
                                    else if (key.ToLower().Equals("coins"))
                                    {
                                        PlayerPrefs.SetInt(Gods.COINS_AMOUNT, Convert.ToInt32(val));
                                        EconomyManager.Instance.UpdateCoins(0);
                                    }
                                    else if (key.ToLower().Equals("gems"))
                                    {
                                        PlayerPrefs.SetInt(Gods.GEMS_AMOUNT, Convert.ToInt32(val));
                                        EconomyManager.Instance.UpdateGems(0);
                                    }
                                    else if (key.ToLower().Equals("bricks"))
                                    {
                                        PlayerPrefs.SetInt(Gods.BRICKS_AMOUNT, Convert.ToInt32(val));
                                        EconomyManager.Instance.UpdateBricks(0);
                                    }
                                    else if (key.ToLower().Equals("cakes"))
                                    {
                                        PlayerPrefs.SetInt(Gods.CAKES_AMOUNT, Convert.ToInt32(val));
                                        EconomyManager.Instance.UpdateCakes(0);
                                    }
                                }
                            }
                        }
                        //Debug.LogError("data fetching success");

                        PanelActivator.Instance.Pop();
                        PanelActivator.Instance.EnableLanding();
                        EnergyManager.Instance.CheckEnergyTimer();
                        EnergyManager.Instance.CheckEnergyValue();
                    },
                    OnEconomyDataFetchError
                );
            }
            else
            {
                PlayerPrefs.SetInt(Gods.SYNCING, 1);

                FetchEconomyData();
            }
        }
        else
        {
            PlayerPrefs.SetInt(Gods.SYNCING, 0);

            Invoke(nameof(LoadData), 0.5f);
            Invoke(nameof(LoadLanding), 0.5f);
        }
    }

    public void OnEconomyDataFetchError(PlayFabError error)
    {
        Debug.LogError("error in data fetching: " + error.ErrorMessage);

        if (currFetchTry++ < maxFetchTries)
        {
            FetchEconomyData();
        }
        else
        {
            currFetchTry = 0;

            PanelActivator.Instance.Pop();
            PanelActivator.Instance.EnableLanding();
            EnergyManager.Instance.CheckEnergyTimer();
            EnergyManager.Instance.CheckEnergyValue();
        }
    }
    #endregion

    #region DELETE PLAYER DATA
    public void DeletePlayerData()
    {
        string playfabId = PlayerPrefs.GetString(Gods.PLAYFAB_ID, string.Empty);
        string entityToken = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty);
        string entityId = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_ID, string.Empty);
        string entityType = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty);
        string sessionTicket = PlayerPrefs.GetString(Gods.CLIENT_SESSION_TICKET, string.Empty);
        var deletePlayerRequest = new DeletePlayerRequest()
        {
            PlayFabId = playfabId,
            AuthenticationContext = new PlayFabAuthenticationContext(sessionTicket, entityToken, playfabId, entityId, entityType),
        };

        PlayFabServerAPI.DeletePlayer(deletePlayerRequest, OnPlayerDeleteSuccess, OnPlayerDeleteFailure);
    }

    private void OnPlayerDeleteSuccess(DeletePlayerResult result)
    {
        loading.SetActive(false);

        Debug.LogError("delete player data successfully");

        SaveData.ClearAllData();
        Destroy(LoginManagers.Instance.gameObject);

        SceneManager.LoadScene("LandingScene");
    }

    private void OnPlayerDeleteFailure(PlayFabError error)
    {
        loading.SetActive(false);

        Debug.LogError("error in deleting player: " + error.ErrorMessage);
    }
    #endregion

    private void LoadData()
    {
        EnergyManager.Instance?.SetEnergyFromPlayerPrefs();
        EconomyManager.Instance?.UpdateCoins(0);
        EconomyManager.Instance?.UpdateGems(0);
        EconomyManager.Instance?.UpdateBricks(0);
        EconomyManager.Instance?.UpdateCakes(0);
    }

    private void LoadLanding()
    {
        PanelActivator.Instance.Pop();
        PanelActivator.Instance.EnableLanding();
    }
}
