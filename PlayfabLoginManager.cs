using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Facebook.Unity;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.AuthenticationModels;
using GooglePlayGames.BasicApi;
using GooglePlayGames;

public class PlayfabLoginManager : MonoBehaviour
{
    private const string emailPattern = @"^([0-9a-zA-Z]([\+\-_\.][0-9a-zA-Z]+)*)+@(([0-9a-zA-Z][-\w]*[0-9a-zA-Z]*\.)+[a-zA-Z0-9]{2,17})$";
    private static readonly List<string> facebookPermissionKeys = new List<string> { "public_profile", "email", "user_friends" };

    public static PlayfabLoginManager Instance;

    [SerializeField] private TMP_InputField loginEmailField;
    [SerializeField] private TMP_InputField loginPasswordField;

    [Space]
    [SerializeField] private TMP_InputField registerEmailField;
    [SerializeField] private TMP_InputField registerUsernameField;
    [SerializeField] private TMP_InputField registerPasswordField;
    [SerializeField] private TMP_InputField registerConfirmPasswordField;

    [Space]
    [SerializeField] private TMP_Text loginErrorMsgText;
    [SerializeField] private TMP_Text registerErrorMsgText;

    [Space]
    [SerializeField] private Button loginBtn;
    [SerializeField] private Button registerBtn;

    [Space]
    [SerializeField] private GameObject mainLoginScreen;
    [SerializeField] private GameObject loadingScreen;

    [HideInInspector] public DateTime serverTime;

    private bool setTimeOnFocus;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        if (PlayerPrefs.GetInt(Gods.INIT_LANGUAGE_SELECTED, 0) == 1)
        {
            CheckLogin();
        }
    }

    public void CheckLogin()
    {
        loadingScreen.SetActive(true);
        if (PlayerPrefs.GetInt(Gods.DIRECT_LOGIN, 0) == 1)
        {
            mainLoginScreen.SetActive(false);

            LoginWithDeviceId();

            PlayerPrefs.SetInt(Gods.DIRECT_LOGIN, 0);
        }
        else
        {
            //has internet
            if (Application.internetReachability != NetworkReachability.NotReachable)
            {
                //not logged in to cloud with this device
                if (!PlayerPrefs.HasKey(Gods.LOGIN_WITH_CLOUD) ||
                    (PlayerPrefs.HasKey(Gods.LOGIN_WITH_CLOUD) && PlayerPrefs.GetInt(Gods.LOGIN_WITH_CLOUD, 0) == 0))
                {
                    int isLoggedIn = PlayerPrefs.GetInt(Gods.IS_PLAYFAB_LOGGED_IN, 0);
                    if (isLoggedIn == 1)
                    {
                        loadingScreen.SetActive(false);
                        mainLoginScreen.SetActive(true);

                        LoginWithDeviceIdInBg();
                    }
                    else
                    {
                        //SetLanguage();
                        LoginWithDeviceId();
                    }
                }
                //logged in with cloud before
                else if (PlayerPrefs.GetInt(Gods.LOGIN_WITH_CLOUD, 0) == 1)
                {
                    if (IsPlayfabLoggedIn())
                    {
                        string loginType = PlayerPrefs.GetString(Gods.PLAYFAB_LOGIN_TYPE, string.Empty);
                        if (loginType == PlayfabLoginType.Email.ToString())
                        {
                            AutoLoginWithEmail();
                        }
                        else if (loginType == PlayfabLoginType.Facebook.ToString()
                            && !string.IsNullOrEmpty(PlayerPrefs.GetString(Gods.FB_ACCESS_TOKEN, string.Empty)))
                        {
                            AutoLoginWithFacebook();
                        }
                        else if (loginType == PlayfabLoginType.Google.ToString()
                            && !string.IsNullOrEmpty(PlayerPrefs.GetString(Gods.GPG_SERVER_AUTH_CODE, string.Empty)))
                        {
                            AutoLoginWithGoogle();
                        }
                        else
                        {
                            LoginWithDeviceId();
                        }
                    }
                    else
                    {
                        //show login screen
                        loadingScreen.SetActive(false);
                        mainLoginScreen.SetActive(true);
                    }
                }
            }
            else
            {
                Invoke(nameof(LoadScene), 1);
            }
        }
    }

    #region PLAYFAB EMAIL LOGIN
    private void AutoLoginWithEmail()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            var request = new LoginWithEmailAddressRequest
            {
                TitleId = PlayFabSettings.TitleId,
                Email = PlayerPrefs.GetString(Gods.PLAYFAB_EMAIL, string.Empty),
                Password = PlayerPrefs.GetString(Gods.PLAYFAB_PASSWORD, string.Empty)
            };
            PlayFabClientAPI.LoginWithEmailAddress(request, OnPlayfabEmailLoginSuccess, OnPlayfabEmailLoginFailure);
        }
    }

    public void LoginWithEmail()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            if (string.IsNullOrEmpty(loginEmailField.text.Trim()) || string.IsNullOrEmpty(loginPasswordField.text.Trim()))
            {
                loginErrorMsgText.text = "Please enter both email and password";
            }
            else
            {
                loginBtn.interactable = false;
                loadingScreen.SetActive(true);

                var request = new LoginWithEmailAddressRequest
                {
                    TitleId = PlayFabSettings.TitleId,
                    Email = loginEmailField.text.Trim(),
                    Password = loginPasswordField.text,
                };
                PlayFabClientAPI.LoginWithEmailAddress(request, OnPlayfabEmailLoginSuccess, OnPlayfabEmailLoginFailure);
            }
        }
        else
        {
            loginErrorMsgText.text = "No internet";
        }
    }

    private void OnPlayfabEmailLoginSuccess(PlayFab.ClientModels.LoginResult result)
    {
        PlayerPrefs.SetString(Gods.PLAYFAB_ID, result.PlayFabId);
        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TOKEN, result.EntityToken.Entity.Id);
        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TYPE, result.EntityToken.Entity.Type);
        PlayerPrefs.SetString(Gods.PLAYFAB_EMAIL, loginEmailField.text.Trim());
        PlayerPrefs.SetString(Gods.PLAYFAB_PASSWORD, loginPasswordField.text);

        PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 1);
        PlayerPrefs.SetString(Gods.CLIENT_SESSION_TICKET, result.AuthenticationContext.ClientSessionTicket);

        if (PlayerPrefs.GetInt(Gods.LOGIN_WITH_CLOUD, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.LOGIN_WITH_CLOUD, 1);
        }

        PlayerPrefs.SetString(Gods.PLAYFAB_LOGIN_TYPE, PlayfabLoginType.Email.ToString());

        Debug.LogError(result.PlayFabId + " logged in successfully");

        LeaderboardManager.Instance.GetLeaderboardData();
        LeaderboardManager.Instance.GetPlayerLeaderboardStats();
        GetPlayerProfile(result.PlayFabId);
        GetServerTime();

        setTimeOnFocus = true;
    }

    private void OnPlayfabEmailLoginFailure(PlayFabError error)
    {
        Debug.LogError("error in login: " + error.ErrorMessage);

        loginErrorMsgText.text = "Login failed. Please try again.";

        loginBtn.interactable = true;
        loadingScreen.SetActive(false);
    }
    #endregion

    #region PLAYFAB FACEBOOK LOGIN
    private void AutoLoginWithFacebook()
    {
        //string accessToken = PlayerPrefs.GetString(Gods.FB_ACCESS_TOKEN, string.Empty);

        //PlayFabClientAPI.LoginWithFacebook(new LoginWithFacebookRequest
        //{
        //    TitleId = PlayFabSettings.TitleId,
        //    CreateAccount = true,
        //    AccessToken = accessToken
        //},
        //        OnPlayfabFbLoginSuccess, OnPlayfabFbLoginFailure);

        LoginWithFacebook();
    }

    public void LoginWithFacebook()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            loadingScreen.SetActive(true);

            InitFb();
        }
    }

    private void InitFb()
    {
        if (FB.IsInitialized)
        {
            OnFacebookInitialized();
        }
        else
        {
            FB.Init(OnFacebookInitialized);
        }
    }

    private void OnFacebookInitialized()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            Debug.LogError("init done: " + FB.IsLoggedIn);
            FB.ActivateApp();

            FB.LogInWithReadPermissions(facebookPermissionKeys, OnFacebookLoggedInSuccess);
        }
    }

    private void OnFacebookLoggedInSuccess(ILoginResult result)
    {
        if (result == null || string.IsNullOrEmpty(result.Error))
        {
            string accessToken = AccessToken.CurrentAccessToken.TokenString;
            PlayerPrefs.SetString(Gods.FB_ACCESS_TOKEN, accessToken);

            if (PlayerPrefs.GetInt(Gods.IS_ACCOUNT_LINKED, 0) == 0)
            {
                LinkFbWithCloud();
            }
            else
            {
                PlayFabClientAPI.LoginWithFacebook(new LoginWithFacebookRequest
                {
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true,
                    AccessToken = accessToken
                },
                    OnPlayfabFbLoginSuccess, OnPlayfabFbLoginFailure);
            }
        }
        else
        {
            Debug.LogError("fb login failure");
        }
    }

    private void OnPlayfabFbLoginSuccess(PlayFab.ClientModels.LoginResult result)
    {
        PlayerPrefs.SetString(Gods.PLAYFAB_ID, result.PlayFabId);
        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TOKEN, result.EntityToken.Entity.Id);
        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TYPE, result.EntityToken.Entity.Type);

        PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 1);
        PlayerPrefs.SetString(Gods.CLIENT_SESSION_TICKET, result.AuthenticationContext.ClientSessionTicket);

        if (PlayerPrefs.GetInt(Gods.LOGIN_WITH_CLOUD, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.LOGIN_WITH_CLOUD, 1);
        }

        PlayerPrefs.SetString(Gods.PLAYFAB_LOGIN_TYPE, PlayfabLoginType.Facebook.ToString());

        Debug.LogError(result.PlayFabId + " logged in via facebook successfully");

        LeaderboardManager.Instance.GetLeaderboardData();
        LeaderboardManager.Instance.GetPlayerLeaderboardStats();
        GetPlayerProfile(result.PlayFabId);
        GetServerTime();

        setTimeOnFocus = true;
    }

    private void OnPlayfabFbLoginFailure(PlayFabError error)
    {
        Debug.LogError("error in facebook login: " + error.ErrorMessage);

        loginErrorMsgText.text = "Login failed. Please try again.";

        loginBtn.interactable = true;
        mainLoginScreen.SetActive(true);
        loadingScreen.SetActive(false);
    }
    #endregion

    #region PLAYFAB GOOGLE LOGIN
    private void AutoLoginWithGoogle()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            loadingScreen.SetActive(true);

            SignInToGPG();
        }
    }

    public void SignInToGPG()
    {
        loadingScreen.SetActive(true);

        PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
        .AddOauthScope("profile")
        .Build();
        PlayGamesPlatform.InitializeInstance(config);
        PlayGamesPlatform.Activate();
        Social.localUser.Authenticate((success) =>
        {
            if (success)
            {
                string code = PlayGamesPlatform.Instance.GetServerAuthCode();
                PlayerPrefs.SetString(Gods.GPG_SERVER_AUTH_CODE, code);

                if (PlayerPrefs.GetInt(Gods.IS_ACCOUNT_LINKED, 0) == 0)
                {
                    LinkGoogleWithCloud();
                }
                else
                {
                    LoginWithGoogle();
                }
            }
            else
            {
                loadingScreen.SetActive(false);

                Debug.LogError("google authentication failed");
            }
        });
    }

    public void LoginWithGoogle()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            string authCode = PlayerPrefs.GetString(Gods.GPG_SERVER_AUTH_CODE, string.Empty);
            if (!string.IsNullOrEmpty(authCode))
            {
                var request = new LoginWithGoogleAccountRequest
                {
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true,
                    ServerAuthCode = authCode,
                };
                PlayFabClientAPI.LoginWithGoogleAccount(request, OnGoogleLoginSuccess, OnGoogleLoginFailure);
            }
            else
            {
                Debug.LogError("login failed. sign in with GPG first");
            }
        }
    }

    private void OnGoogleLoginSuccess(PlayFab.ClientModels.LoginResult result)
    {
        PlayerPrefs.SetString(Gods.PLAYFAB_ID, result.PlayFabId);
        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TOKEN, result.EntityToken.Entity.Id);
        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TYPE, result.EntityToken.Entity.Type);
        PlayerPrefs.SetString(Gods.GPG_SERVER_AUTH_CODE, "");

        PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 1);
        PlayerPrefs.SetString(Gods.CLIENT_SESSION_TICKET, result.AuthenticationContext.ClientSessionTicket);

        if (PlayerPrefs.GetInt(Gods.LOGIN_WITH_CLOUD, 0) == 0)
        {
            PlayerPrefs.SetInt(Gods.LOGIN_WITH_CLOUD, 1);
        }

        PlayerPrefs.SetString(Gods.PLAYFAB_LOGIN_TYPE, PlayfabLoginType.Google.ToString());

        Debug.LogError(result.PlayFabId + " logged in successfully");

        LeaderboardManager.Instance.GetLeaderboardData();
        LeaderboardManager.Instance.GetPlayerLeaderboardStats();
        GetPlayerProfile(result.PlayFabId);
        GetServerTime();

        setTimeOnFocus = true;
    }

    private void OnGoogleLoginFailure(PlayFabError error)
    {
        Debug.LogError("error in login: " + error.ErrorMessage);

        loadingScreen.SetActive(false);
    }
    #endregion

    #region PLAYFAB LOGIN WITH DEVICE ID
    public string GetDeviceId()
    {
#if UNITY_EDITOR
        //return SystemInfo.deviceUniqueIdentifier;
        return "11111111111";
        //return "987654321";
#elif UNITY_ANDROID
        AndroidJavaClass clsUnity = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject objActivity = clsUnity.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject objResolver = objActivity.Call<AndroidJavaObject>("getContentResolver");
        AndroidJavaClass clsSecure = new AndroidJavaClass("android.provider.Settings$Secure");
        return clsSecure.CallStatic<string>("getString", objResolver, "android_id");
#elif UNITY_IOS
    	return UnityEngine.iOS.Device.vendorIdentifier;
#endif
    }

    public void LoginWithDeviceId()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            string deviceId = GetDeviceId();
            if (!string.IsNullOrEmpty(deviceId))
            {
                loadingScreen.SetActive(true);

#if UNITY_EDITOR
                var customLoginRequest = new LoginWithCustomIDRequest
                {
                    CustomId = deviceId,
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true
                };

                PlayFabClientAPI.LoginWithCustomID(customLoginRequest, OnDeviceIdLoginSuccess, OnDeviceIdLoginFailure);
#elif UNITY_ANDROID
                var androidLoginRequest = new LoginWithAndroidDeviceIDRequest
                {
                    AndroidDeviceId = deviceId,
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true
                };

                PlayFabClientAPI.LoginWithAndroidDeviceID(androidLoginRequest, OnDeviceIdLoginSuccess, OnDeviceIdLoginFailure);
#elif UNITY_IOS
                var iosLoginRequest = new LoginWithIOSDeviceIDRequest
                {
                    DeviceId = deviceId,
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true
                };

                PlayFabClientAPI.LoginWithIOSDeviceID(iosLoginRequest, OnDeviceIdLoginSuccess, OnDeviceIdLoginFailure);
#endif
            }
            else
            {
                loadingScreen.SetActive(false);
                mainLoginScreen.SetActive(true);
            }
        }
    }

    private void OnDeviceIdLoginSuccess(PlayFab.ClientModels.LoginResult result)
    {
        if (PlayerPrefs.GetInt(Gods.IS_PLAYFAB_LOGGED_IN, 0) == 0)
        {
            PlayerPrefs.SetString(Gods.PLAYFAB_ID, result.PlayFabId);
            PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TOKEN, result.EntityToken.Entity.Id);
            PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TYPE, result.EntityToken.Entity.Type);

            PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 1);
        }

        PlayerPrefs.SetString(Gods.CLIENT_SESSION_TICKET, result.AuthenticationContext.ClientSessionTicket);

        if (PlayerPrefs.GetString(Gods.PLAYFAB_DISPLAY_NAME, string.Empty).Equals(string.Empty)
            || PlayerPrefs.GetString(Gods.PLAYFAB_DISPLAY_NAME, string.Empty).Equals(""))
        {
            UpdateDisplayName("user" + result.PlayFabId);
        }

        PlayerPrefs.SetString(Gods.PLAYFAB_LOGIN_TYPE, PlayfabLoginType.Device.ToString());

        Debug.LogError(result.PlayFabId + " logged in with device successfully");

        LeaderboardManager.Instance.GetLeaderboardData();
        LeaderboardManager.Instance.GetPlayerLeaderboardStats();
        GetPlayerProfile(result.PlayFabId);
        GetServerTime();

        setTimeOnFocus = true;
    }

    private void OnDeviceIdLoginFailure(PlayFabError error)
    {
        Debug.LogError("error in login: " + error.ErrorMessage);

        loginErrorMsgText.text = "Login failed. Please try again.";

        loginBtn.interactable = true;
        loadingScreen.SetActive(false);
        mainLoginScreen.SetActive(true);
    }
    #endregion

    #region PLAYFAB LOGIN WITH DEVICE ID IN BG

    public void LoginWithDeviceIdInBg()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            string deviceId = GetDeviceId();
            if (!string.IsNullOrEmpty(deviceId))
            {
#if UNITY_EDITOR
                var customLoginRequest = new LoginWithCustomIDRequest
                {
                    CustomId = deviceId,
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true
                };

                PlayFabClientAPI.LoginWithCustomID(customLoginRequest, OnDeviceIdLoginInBgSuccess, OnDeviceIdLoginInBgFailure);
#elif UNITY_ANDROID
                var androidLoginRequest = new LoginWithAndroidDeviceIDRequest
                {
                    AndroidDeviceId = deviceId,
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true
                };

                PlayFabClientAPI.LoginWithAndroidDeviceID(androidLoginRequest, OnDeviceIdLoginInBgSuccess, OnDeviceIdLoginInBgFailure);
#elif UNITY_IOS
                var iosLoginRequest = new LoginWithIOSDeviceIDRequest
                {
                    DeviceId = deviceId,
                    TitleId = PlayFabSettings.TitleId,
                    CreateAccount = true
                };

                PlayFabClientAPI.LoginWithIOSDeviceID(iosLoginRequest, OnDeviceIdLoginSuccess, OnDeviceIdLoginFailure);
#endif
            }
            else
            {
                loadingScreen.SetActive(false);
                mainLoginScreen.SetActive(true);
            }
        }
    }

    private void OnDeviceIdLoginInBgSuccess(PlayFab.ClientModels.LoginResult result)
    {
        if (PlayerPrefs.GetInt(Gods.IS_PLAYFAB_LOGGED_IN, 0) == 0)
        {
            PlayerPrefs.SetString(Gods.PLAYFAB_ID, result.PlayFabId);
            PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TOKEN, result.EntityToken.Entity.Id);
            PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TYPE, result.EntityToken.Entity.Type);

            PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 1);
        }

        PlayerPrefs.SetString(Gods.CLIENT_SESSION_TICKET, result.AuthenticationContext.ClientSessionTicket);

        if (PlayerPrefs.GetString(Gods.PLAYFAB_DISPLAY_NAME, string.Empty).Equals(string.Empty)
            || PlayerPrefs.GetString(Gods.PLAYFAB_DISPLAY_NAME, string.Empty).Equals(""))
        {
            UpdateDisplayName("user" + result.PlayFabId);
        }

        PlayerPrefs.SetString(Gods.PLAYFAB_LOGIN_TYPE, PlayfabLoginType.Device.ToString());

        Debug.LogError(result.PlayFabId + " logged in with device in bg successfully");
    }

    private void OnDeviceIdLoginInBgFailure(PlayFabError error)
    {
        Debug.LogError("error in bg login: " + error.ErrorMessage);
    }
    #endregion

    #region PLAYER PROFILE
    private void GetPlayerProfile(string id)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            var request = new GetPlayerProfileRequest { PlayFabId = id };
            PlayFabClientAPI.GetPlayerProfile(request, OnPlayerProfileFetchSuccess, OnPlayerProfileFetchFailed);
        }
    }

    private void OnPlayerProfileFetchSuccess(GetPlayerProfileResult result)
    {
        PlayerPrefs.SetString(Gods.PLAYFAB_DISPLAY_NAME, result.PlayerProfile.DisplayName);
    }

    private void OnPlayerProfileFetchFailed(PlayFabError error)
    {
        Debug.LogError("error in fetching player profile: " + error.ErrorMessage);
    }
    #endregion

    #region PLAYFAB REGISTER
    public void RegisterWithPlayfab()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            if (string.IsNullOrEmpty(registerEmailField.text.Trim()) || string.IsNullOrEmpty(registerPasswordField.text.Trim())
            || string.IsNullOrEmpty(registerConfirmPasswordField.text.Trim()))
            {
                registerErrorMsgText.text = "Please enter email, password and confirm password";
            }
            else if (!registerPasswordField.text.Equals(registerConfirmPasswordField.text))
            {
                registerErrorMsgText.text = "Password and confirm password does not match";
            }
            else
            {
                registerBtn.interactable = false;
                loadingScreen.SetActive(true);

                var request = new RegisterPlayFabUserRequest
                {
                    TitleId = PlayFabSettings.TitleId,
                    Email = registerEmailField.text.Trim(),
                    Username = registerUsernameField.text.Trim(),
                    Password = registerPasswordField.text,
                };
                PlayFabClientAPI.RegisterPlayFabUser(request, OnPlayfabRegisterSuccess, OnPlayfabRegisterFailure);
            }
        }
        else
        {
            registerErrorMsgText.text = "No inetrnet";
        }
    }

    private void OnPlayfabRegisterSuccess(RegisterPlayFabUserResult result)
    {
        registerErrorMsgText.text = "Register successful.";

        UpdateDisplayName(registerUsernameField.text.Trim());

        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TOKEN, result.EntityToken.Entity.Id);
        PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TYPE, result.EntityToken.Entity.Type);
        PlayerPrefs.SetString(Gods.CLIENT_SESSION_TICKET, result.AuthenticationContext.ClientSessionTicket);

        Debug.LogError(result.PlayFabId + " sign up successfully");
    }

    private void OnPlayfabRegisterFailure(PlayFabError error)
    {
        registerErrorMsgText.text = "Register to playfab failed. Please try again.";

        registerBtn.interactable = true;
        loadingScreen.SetActive(false);

        Debug.LogError("error in register user: " + error.ErrorMessage);
    }
    #endregion

    #region ACCOUNT RECOVERY
    public void SendAccountRecoveryEmail(string email)
    {
        var request = new SendAccountRecoveryEmailRequest
        {
            Email = email,
            TitleId = PlayFabSettings.TitleId
        };

        PlayFabClientAPI.SendAccountRecoveryEmail(request, OnRecoveryEmailSendSuccess, OnRecoveryEmailSendFailure);
    }

    private void OnRecoveryEmailSendSuccess(SendAccountRecoveryEmailResult result)
    {
        Debug.LogError("recovery email sent successfully");
    }

    private void OnRecoveryEmailSendFailure(PlayFabError error)
    {
        Debug.LogError("recovery email send failed");
    }
    #endregion

    #region DISPLAY NAME UPDATE
    private void UpdateDisplayName(string name)
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            PlayFabClientAPI.UpdateUserTitleDisplayName(new UpdateUserTitleDisplayNameRequest { DisplayName = name }, OnDisplayNameSuccess, OnDisplayNameFailure);
        }
    }

    private void OnDisplayNameSuccess(UpdateUserTitleDisplayNameResult result)
    {
        Debug.LogError("display name: " + result.DisplayName);

        PlayerPrefs.SetString(Gods.PLAYFAB_DISPLAY_NAME, result.DisplayName.ToString());

        //loadingScreen.SetActive(false);
    }

    private void OnDisplayNameFailure(PlayFabError error)
    {
        Debug.LogError("error in display name update: " + error.ErrorMessage);
        loadingScreen.SetActive(false);
    }
    #endregion

    #region ENTITY TOKEN FETCH
    private void GetEntityTokenDetails()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            PlayFabAuthenticationAPI.GetEntityToken(new GetEntityTokenRequest(),
            (entityResult) =>
            {
                PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TOKEN, entityResult.Entity.Id);
                PlayerPrefs.SetString(Gods.PLAYFAB_ENTITY_TYPE, entityResult.Entity.Type);
            }, OnEntityTokenFailure);
        }
    }

    private void OnEntityTokenFailure(PlayFabError error)
    {
        Debug.LogError("error in entity token fetching: " + error.ErrorMessage);
    }
    #endregion

    #region SERVER TIME FETCH
    bool setTime = true;
    public void GetServerTime()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            PlayFabServerAPI.GetTime(new PlayFab.ServerModels.GetTimeRequest(), OnGetTimeSuccess, OnGetTimeFailure);
        }
    }

    private void OnGetTimeSuccess(PlayFab.ServerModels.GetTimeResult result)
    {
        serverTime = result.Time;

        if (setTime)
        {
            setTime = false;

            TimeManager.Instance.StartTimer(result.Time);
        }
        else
        {
            //if (!inApp)
            //{
            //    outTime = result.Time;
            //}
            //else
            //{
            //    inTime = result.Time;

            //    if (TimeManager.Instance)
            //    {
            //        TimeManager.Instance.AddTime((int)(inTime.Subtract(outTime).TotalSeconds));
            //    }
            //}
        }
    }

    private void OnGetTimeFailure(PlayFabError error)
    {
        Debug.LogError("error in server time fetching: " + error.ErrorMessage);
    }

    private void OnApplicationFocus(bool focus)
    {
        if (setTimeOnFocus)
        {
            GetServerTime();
        }
    }
    #endregion

    #region LINKING DEVICE ID
    public void LinkDeviceIdWithCloud()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
            string deviceId = GetDeviceId();
            if (!string.IsNullOrEmpty(deviceId))
            {
#if UNITY_ANDROID
                var androidRequest = new LinkAndroidDeviceIDRequest { AndroidDeviceId = deviceId };

                PlayFabClientAPI.LinkAndroidDeviceID(androidRequest, OnLinkingAndroidDeviceSuccess, OnLinkingDeviceFailure);
#elif UNITY_IOS
            var iosRequest = new LinkIOSDeviceIDRequest { DeviceId = deviceId };

            PlayFabClientAPI.LinkIOSDeviceID(iosRequest, OnLinkingIosDeviceSuccess, OnLinkingDeviceFailure);
#endif
            }
        }
        else
        {
            Debug.LogError("no internet connection");
        }
    }

    private void OnLinkingAndroidDeviceSuccess(LinkAndroidDeviceIDResult result)
    {
        Debug.LogError("linked device id successfully");
    }

    private void OnLinkingIosDeviceSuccess(LinkIOSDeviceIDResult result)
    {
        Debug.LogError("linked device id successfully");
    }

    private void OnLinkingDeviceFailure(PlayFabError error)
    {
        if (error.ErrorMessage.Contains("already linked"))
        {
            Debug.LogError("device linking failed. this account is already linked");
        }
        else
        {
            Debug.LogError("device linking failed");
        }
    }
    #endregion

    #region LINKING FACEBOOK ACCOUNT
    public void LinkFbWithCloud()
    {
        string accessToken = PlayerPrefs.GetString(Gods.FB_ACCESS_TOKEN, string.Empty);
        if (!string.IsNullOrEmpty(accessToken))
        {
            var request = new LinkFacebookAccountRequest { AccessToken = accessToken, ForceLink = false };
            PlayFabClientAPI.LinkFacebookAccount(request, OnFbLinkingSuccess, OnFbLinkingFailure);
        }
        else
        {
            Debug.LogError("FB access token not found");
        }
    }

    private void OnFbLinkingSuccess(LinkFacebookAccountResult result)
    {
        Debug.LogError("FB account linking successfull");

        AfterFbLinking();
    }

    private void OnFbLinkingFailure(PlayFabError error)
    {
        if (error.ErrorMessage.Contains("already linked"))
        {
            Debug.LogError("FB account linking failed. this account is already linked");

            //loadingScreen.SetActive(false);

            AfterFbLinking();
        }
        else
        {
            Debug.LogError("FB account linking failed");
        }
    }

    private void AfterFbLinking()
    {
        PlayerPrefs.SetInt(Gods.IS_ACCOUNT_LINKED, 1);

        PlayFabClientAPI.LoginWithFacebook(new LoginWithFacebookRequest
        {
            TitleId = PlayFabSettings.TitleId,
            AccessToken = PlayerPrefs.GetString(Gods.FB_ACCESS_TOKEN, string.Empty)
        },
                OnPlayfabFbLoginSuccess, OnPlayfabFbLoginFailure);
    }
    #endregion

    #region LINKING GOOGLE ACCOUNT
    public void LinkGoogleWithCloud()
    {
        string authCode = PlayerPrefs.GetString(Gods.GPG_SERVER_AUTH_CODE, string.Empty);
        if (!string.IsNullOrEmpty(authCode))
        {
            var request = new LinkGoogleAccountRequest { ServerAuthCode = authCode, ForceLink = false };
            PlayFabClientAPI.LinkGoogleAccount(request, OnGoogleLinkingSuccess, OnGoogleLinkingFailure);
        }
        else
        {
            Debug.LogError("Google server autuh code not found");
        }
    }

    private void OnGoogleLinkingSuccess(LinkGoogleAccountResult result)
    {
        Debug.LogError("Google account linking successfull");

        AfterGoogleLinking();
    }

    private void OnGoogleLinkingFailure(PlayFabError error)
    {
        if (error.ErrorMessage.Contains("already linked"))
        {
            Debug.LogError("Google account linking failed. this account is already linked");

            //loadingScreen.SetActive(false);

            AfterGoogleLinking();
        }
        else
        {
            Debug.LogError("Google account linking failed");
        }
    }

    private void AfterGoogleLinking()
    {
        PlayerPrefs.SetInt(Gods.IS_ACCOUNT_LINKED, 1);

        var request = new LoginWithGoogleAccountRequest
        {
            TitleId = PlayFabSettings.TitleId,
            ServerAuthCode = PlayerPrefs.GetString(Gods.GPG_SERVER_AUTH_CODE, string.Empty)
        };
        PlayFabClientAPI.LoginWithGoogleAccount(request, OnGoogleLoginSuccess, OnGoogleLoginFailure);
    }
    #endregion

    #region UN-LINKING DEVICE ID
    public void UnlinkDeviceId()
    {
        if (Application.internetReachability != NetworkReachability.NotReachable)
        {
#if UNITY_ANDROID
            UnlinkAndroidDeviceIDRequest androidRequest = new UnlinkAndroidDeviceIDRequest();
            PlayFabClientAPI.UnlinkAndroidDeviceID(androidRequest, OnUnlinkingAndroidDeviceSuccess, OnUnlinkingDeviceFailure);
#elif UNITY_IOS
            UnlinkIOSDeviceIDRequest iosRequest = new UnlinkIOSDeviceIDRequest();
            PlayFabClientAPI.UnlinkIOSDeviceID(iosRequest, OnUnlinkingIosDeviceSuccess, OnUnlinkingDeviceFailure);
#endif
        }
        else
        {
            Debug.LogError("no internet connection");
        }
    }

    private void OnUnlinkingAndroidDeviceSuccess(UnlinkAndroidDeviceIDResult result)
    {
        Debug.LogError("unlinked device id successfully");
    }

    private void OnUnlinkingIosDeviceSuccess(UnlinkIOSDeviceIDResult result)
    {
        Debug.LogError("unlinked device id successfully");
    }

    private void OnUnlinkingDeviceFailure(PlayFabError error)
    {
        Debug.LogError("device unlinking failed");
    }
    #endregion

    public void Logout()
    {
        if (IsPlayfabLoggedIn())
        {
            string loginType = PlayerPrefs.GetString(Gods.PLAYFAB_LOGIN_TYPE, string.Empty);
            if (loginType == PlayfabLoginType.Email.ToString())
            {
                PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 0);
                PlayerPrefs.SetInt(Gods.LOGIN_WITH_CLOUD, 0);
            }
            else if (loginType == PlayfabLoginType.Facebook.ToString())
            {
                PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 0);

                if (FB.IsInitialized || FB.IsLoggedIn)
                {
                    PlayerPrefs.SetString(Gods.FB_ACCESS_TOKEN, string.Empty);
                    PlayerPrefs.SetInt(Gods.LOGIN_WITH_CLOUD, 0);

                    FB.LogOut();
                }
            }
            else if (loginType == PlayfabLoginType.Google.ToString())
            {
                PlayerPrefs.SetInt(Gods.IS_PLAYFAB_LOGGED_IN, 0);
                PlayerPrefs.SetInt(Gods.LOGIN_WITH_CLOUD, 0);

                PlayerPrefs.SetString(Gods.GPG_SERVER_AUTH_CODE, string.Empty);
            }
        }
    }

    public bool IsPlayfabLoggedIn()
    {
        return PlayerPrefs.GetInt(Gods.IS_PLAYFAB_LOGGED_IN, 0) == 1;
    }

    public bool IsCloudLoggedIn()
    {
        return PlayerPrefs.GetInt(Gods.LOGIN_WITH_CLOUD, 0) == 1;
    }

    private void LoadScene()
    {
        SceneLoadingManager.Instance?.LoadScene(SceneType.MenuScene);
    }

    public bool ValidateEmail(string email)
    {
        return Regex.IsMatch(email, emailPattern);
    }

    public bool ValidatePassword(string password1, string password2)
    {
        return ((password1 == password2) && password1.Length >= 8);
    }

    private int tryCount = 0;
    public void ConfirmDeviceLoginBtn()
    {
        loadingScreen.SetActive(true);

        if (PlayFabClientAPI.IsClientLoggedIn())
        {
            LeaderboardManager.Instance.GetLeaderboardData();
            LeaderboardManager.Instance.GetPlayerLeaderboardStats();
            GetPlayerProfile(PlayerPrefs.GetString(Gods.PLAYFAB_ID, string.Empty));
            GetServerTime();

            setTimeOnFocus = true;
        }
        else
        {
            //all tries done
            if (tryCount++ >= 3)
            {
                LoginWithDeviceId();
            }
            else
            {
                LoginWithDeviceIdInBg();

                Invoke(nameof(ConfirmDeviceLoginBtn), 2);
            }
        }

    }

    public void ConfirmFbLoginBtn()
    {
        PlayerPrefs.SetInt(Gods.IS_ACCOUNT_LINKED, 1);

        LoginWithFacebook();
    }

    public void ConfirmGoogleLoginBtn()
    {
        PlayerPrefs.SetInt(Gods.IS_ACCOUNT_LINKED, 1);

        SignInToGPG();
    }

    private void SetLanguage()
    {
        SystemLanguage systemLanguage = Application.systemLanguage;
        if (systemLanguage == SystemLanguage.Spanish)
        {
            PlayerPrefs.SetString(Gods.SELECTED_LANGUAGE, "Español");
        }
        else if (systemLanguage == SystemLanguage.French)
        {
            PlayerPrefs.SetString(Gods.SELECTED_LANGUAGE, "Français");
        }
        else if (systemLanguage == SystemLanguage.German)
        {
            PlayerPrefs.SetString(Gods.SELECTED_LANGUAGE, "Deutsch");
        }
        else if (systemLanguage == SystemLanguage.Italian)
        {
            PlayerPrefs.SetString(Gods.SELECTED_LANGUAGE, "Italiano");
        }
        else if (systemLanguage == SystemLanguage.Russian)
        {
            PlayerPrefs.SetString(Gods.SELECTED_LANGUAGE, "Pусский");
        }
        else if (systemLanguage == SystemLanguage.Portuguese)
        {
            PlayerPrefs.SetString(Gods.SELECTED_LANGUAGE, "Português");
        }
        else
        {
            PlayerPrefs.SetString(Gods.SELECTED_LANGUAGE, "English");
        }
    }
}
