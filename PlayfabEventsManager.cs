using PlayFab;
using PlayFab.ClientModels;
using PlayFab.EventsModels;
using System.Collections.Generic;
using UnityEngine;

public class PlayfabEventsManager : MonoBehaviour
{
    public static PlayfabEventsManager Instance;

    public delegate void EventsHandler();
    public static event EventsHandler OnAppStartEvents;
    public static event EventsHandler OnAppCloseEvents;
    public static event EventsHandler OnGamePlayEvents;
    public static event EventsHandler OnGameOverEvents;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (PlayfabLoginManager.Instance.IsPlayfabLoggedIn())
        {
            OnAppStartEvents?.Invoke();
        }
    }

    //private void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.T))
    //    {
    //        LogCustomTitleEvent("player_event_test", new Dictionary<string, object>() { { "test_1", 1 } });
    //    }

    //    if (Input.GetKeyDown(KeyCode.P))
    //    {
    //        LogCustomPlayerEvent("player_event_test", new Dictionary<string, object>() { { "test_1", 1 } });
    //    }
    //}

    private void OnApplicationQuit()
    {
        if (PlayfabEventsManager.Instance && PlayfabLoginManager.Instance.IsPlayfabLoggedIn())
        {
            OnAppCloseEvents?.Invoke();
        }
    }

    public void OnPlay()
    {
        if (PlayfabEventsManager.Instance && PlayfabLoginManager.Instance.IsPlayfabLoggedIn())
        {
            OnGamePlayEvents?.Invoke();
        }
    }

    public void OnGameOver()
    {
        if (PlayfabEventsManager.Instance && PlayfabLoginManager.Instance.IsPlayfabLoggedIn())
        {
            OnGameOverEvents?.Invoke();
        }
    }

    #region CUSTOM EVENT
    public void LogCustomTitleEvent(string eventName, Dictionary<string, object> param)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            PlayFabClientAPI.WriteTitleEvent(new WriteTitleEventRequest()
            {
                Body = param,
                EventName = eventName
            },
            result => Debug.Log("title event logged successfully"),
            error => Debug.LogError(error.GenerateErrorReport()));
        }
    }

    public void LogCustomPlayerEvent(string eventName, Dictionary<string, object> param)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            PlayFabClientAPI.WritePlayerEvent(new WriteClientPlayerEventRequest()
            {
                Body = param,
                EventName = eventName
            },
            result => Debug.Log("player event logged successfully"),
            error => Debug.LogError(error.GenerateErrorReport()));
        }
    }
    #endregion
}
