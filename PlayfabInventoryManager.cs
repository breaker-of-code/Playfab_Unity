using PlayFab;
using UnityEngine;
using System;
using System.Collections.Generic;
using PlayFab.ServerModels;

[Serializable]
public class PlayfabInventroyModel
{
    public string id;
    public string instanceId;
    public string name;
    public int count;
}

[Serializable]
public class GrantItemModel
{
    public string itemId;
    public int countToAdd;
}

public class PlayfabInventoryManager : MonoBehaviour
{
    public static PlayfabInventoryManager Instance;

    [SerializeField] private List<PlayfabInventroyModel> inventory;
    private List<GrantItemModel> grantItems;

    [SerializeField] private int maxTries = 3;
    private int getTries;
    private int addTries;
    private int removeTries;

    private string addingItem;
    private int addingCount;
    private string removingItem;
    private int removingCount;

    //[Space]
    //[SerializeField] private string itemId;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void Start()
    {
        grantItems = new List<GrantItemModel>();

        GetUserInventory();
    }

    //private void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.I))
    //    {
    //        AddItemToInventory(itemId, 100);
    //    }
    //}

    #region GET INVENTORY
    private void GetUserInventory()
    {
        if (!PlayfabLoginManager.Instance.IsPlayfabLoggedIn()
            || Application.internetReachability == NetworkReachability.NotReachable) return;

        string playfabId = PlayerPrefs.GetString(Gods.PLAYFAB_ID, string.Empty);
        string entityToken = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty);
        string entityId = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_ID, string.Empty);
        string entityType = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty);
        string sessionTicket = PlayerPrefs.GetString(Gods.CLIENT_SESSION_TICKET, string.Empty);

        var request = new GetUserInventoryRequest
        {
            AuthenticationContext = new PlayFabAuthenticationContext(sessionTicket, entityToken, playfabId, entityId, entityType),
            PlayFabId = playfabId,
        };
        PlayFabServerAPI.GetUserInventory(request, OnInventoryFetchSuccess, OnInventoryFetchFail);
    }

    private void OnInventoryFetchSuccess(GetUserInventoryResult result)
    {
        //Debug.LogError("inventory fetched successfully: " + result.Inventory.Count);

        getTries = 0;
        inventory = new List<PlayfabInventroyModel>();
        inventory.Clear();

        foreach (var item in result.Inventory)
        {
            PlayfabInventroyModel inItem = new PlayfabInventroyModel()
            {
                id = item.ItemId,
                instanceId = item.ItemInstanceId,
                name = item.DisplayName,
                count = (int)item.RemainingUses,
            };

            //Debug.LogError(inItem.id + " has " + inItem.count + " : " + item.ItemInstanceId);

            inventory.Add(inItem);
        }

        ShopManager.Instance.CheckShopSpawn();
    }

    private void OnInventoryFetchFail(PlayFabError error)
    {
        Debug.LogError("error in fetching player inventory: " + error.ErrorMessage);

        if (getTries < maxTries)
        {
            getTries++;

            GetUserInventory();
        }
        else
        {
            getTries = 0;
        }
    }
    #endregion

    #region GRANT ITEM IN INVENTORY
    private void GrantItemToInventory(string itemId)
    {
        if (!PlayfabLoginManager.Instance.IsPlayfabLoggedIn()
            || Application.internetReachability == NetworkReachability.NotReachable) return;

        string playfabId = PlayerPrefs.GetString(Gods.PLAYFAB_ID, string.Empty);
        string entityToken = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty);
        string entityId = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_ID, string.Empty);
        string entityType = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty);
        string sessionTicket = PlayerPrefs.GetString(Gods.CLIENT_SESSION_TICKET, string.Empty);

        var request = new GrantItemsToUserRequest
        {
            AuthenticationContext = new PlayFabAuthenticationContext(sessionTicket, entityToken, playfabId, entityId, entityType),
            PlayFabId = playfabId,
            ItemIds = new List<string>() { itemId },
        };
        PlayFabServerAPI.GrantItemsToUser(request, OnGrantToInventorySuccess, OnGrantToInventoryFail);
    }

    private void OnGrantToInventorySuccess(PlayFab.ServerModels.GrantItemsToUserResult result)
    {
        //Debug.LogError("item granted successfully");

        addTries = 0;

        RefreshInventory();

        string grantItemId = result.ItemGrantResults[0].ItemId;
        string instanceId = result.ItemGrantResults[0].ItemInstanceId;
        if (grantItems.Count > 0 && GrantItemExist(grantItemId))
        {
            int index = GetGrantItemIndex(grantItemId);
            GrantItemModel grantItem = grantItems[index];
            
            AddItemToInventory(grantItem.itemId, grantItem.countToAdd - 2);
        }

        UpdateInventory(grantItemId, 1, instanceId);
    }

    private void OnGrantToInventoryFail(PlayFabError error)
    {
        Debug.LogError("error in granting item: " + error.ErrorMessage);

        if (addTries < maxTries)
        {
            addTries++;

            GrantItemToInventory(addingItem);
        }
        else
        {
            addTries = 0;
        }
    }

    private bool GrantItemExist(string id)
    {
        bool exists = false;
        foreach (var item in grantItems)
        {
            if (item.itemId.Equals(id))
            {
                exists = true;

                break;
            }
        }

        return exists;
    }

    private int GetGrantItemIndex(string id)
    {
        int index = 0;
        foreach (var item in grantItems)
        {
            if (item.itemId.Equals(id))
            {
                break;
            }

            index++;
        }

        return index;
    }
    #endregion

    #region ADD ITEM IN INVENTORY
    public void AddItemToInventory(string itemId, int countToAdd = 1)
    {
        //Debug.LogError("adding " + countToAdd + " instances of " + itemId);
        if (!PlayfabLoginManager.Instance.IsPlayfabLoggedIn()
            || Application.internetReachability == NetworkReachability.NotReachable) return;

        if (countToAdd < 0)
        {
            RemoveFromInventory(itemId, Mathf.Abs(countToAdd));

            return;
        }

        addingItem = itemId;
        addingCount = countToAdd;

        if (IsItemExist(itemId))
        {
            string playfabId = PlayerPrefs.GetString(Gods.PLAYFAB_ID, string.Empty);
            string entityToken = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty);
            string entityId = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_ID, string.Empty);
            string entityType = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty);
            string sessionTicket = PlayerPrefs.GetString(Gods.CLIENT_SESSION_TICKET, string.Empty);
            
            var request = new ModifyItemUsesRequest
            {
                AuthenticationContext = new PlayFabAuthenticationContext(sessionTicket, entityToken, playfabId, entityId, entityType),
                PlayFabId = playfabId,
                ItemInstanceId = GetItemInstanceId(itemId),
                UsesToAdd = countToAdd,
            };
            PlayFabServerAPI.ModifyItemUses(request, OnAddToInventorySuccess, OnAddToInventoryFail);

            UpdateInventory(itemId, countToAdd);
        }
        else
        {
            GrantItemToInventory(itemId);

            if (countToAdd > 1)
            {
                grantItems.Add(new GrantItemModel() { itemId = itemId, countToAdd = countToAdd });
            }
        }
    }

    private void OnAddToInventorySuccess(PlayFab.ServerModels.ModifyItemUsesResult result)
    {
        //Debug.LogError("item added successfully");

        addTries = 0;

        RefreshInventory();
    }

    private void OnAddToInventoryFail(PlayFabError error)
    {
        Debug.LogError("error in adding item: " + error.ErrorMessage);

        if (addTries < maxTries)
        {
            addTries++;

            AddItemToInventory(addingItem, addingCount);
        }
        else
        {
            addTries = 0;
        }
    }
    #endregion

    #region REMOVE ITEM FROM INVENTORY
    public void RemoveFromInventory(string itemId, int uses)
    {
        //Debug.LogError("removing " + uses + " instances of " + itemId);
        if (!PlayfabLoginManager.Instance.IsPlayfabLoggedIn()
            || Application.internetReachability == NetworkReachability.NotReachable) return;

        if (IsItemExist(itemId) && GetItemCount(itemId) >= uses)
        {
            removingItem = itemId;
            removingCount = uses;

            string playfabId = PlayerPrefs.GetString(Gods.PLAYFAB_ID, string.Empty);
            string entityToken = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TOKEN, string.Empty);
            string entityId = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_ID, string.Empty);
            string entityType = PlayerPrefs.GetString(Gods.PLAYFAB_ENTITY_TYPE, string.Empty);
            string sessionTicket = PlayerPrefs.GetString(Gods.CLIENT_SESSION_TICKET, string.Empty);

            var request = new ModifyItemUsesRequest
            {
                AuthenticationContext = new PlayFabAuthenticationContext(sessionTicket, entityToken, playfabId, entityId, entityType),
                PlayFabId = playfabId,
                ItemInstanceId = GetItemInstanceId(itemId),
                UsesToAdd = -uses,
            };
            PlayFabServerAPI.ModifyItemUses(request, OnRemoveFromInventorySuccess, OnRemoveFromInventoryFail);

            UpdateInventory(itemId, -uses);
        }
        else
        {
            Debug.LogError("not enough items");

            FloatingMsgController.Instance.ShowMsg("menu.warning.notEnoughItems");
        }
    }

    private void OnRemoveFromInventorySuccess(PlayFab.ServerModels.ModifyItemUsesResult result)
    {
        //Debug.LogError("removed item successfully");

        removeTries = 0;

        RefreshInventory();
    }

    private void OnRemoveFromInventoryFail(PlayFabError error)
    {
        Debug.LogError("error in removing item: " + error.ErrorMessage);

        if (removeTries < maxTries)
        {
            removeTries++;

            RemoveFromInventory(removingItem, removingCount);
        }
        else
        {
            removeTries = 0;
        }
    }
    #endregion

    public bool IsItemExist(string itemId)
    {
        bool exist = false;
        try
        {
            PlayfabInventroyModel item = inventory.Find(x => x.id.Equals(itemId));
            if (item != null) exist = true;
        }
        catch { }

        return exist;
    }

    public int GetItemCount(string itemId)
    {
        int? count = 0;
        int value = 0;
        try
        {
            count = inventory.Find(x => x.id.Equals(itemId)).count;
            value = (count == null) ? default(int) : count.Value;
        }
        catch { }

        return value;
    }

    public string GetItemInstanceId(string itemId)
    {
        string id = string.Empty;
        try { id = inventory.Find(x => x.id.Equals(itemId)).instanceId; }
        catch { }

        return id;
    }

    public void RefreshInventory()
    {
        //GetUserInventory();
    }

    private void UpdateInventory(string id, int value, string instanceId = "")
    {
        try
        {
            PlayfabInventroyModel item = inventory.Find(x => x.id.Equals(id));
            if (item != null)
            {
                inventory.Find(x => x.id.Equals(id)).count += value;
            }
            else
            {
                inventory.Add(new PlayfabInventroyModel() { id = id, count = value, name = id, instanceId = instanceId });
            }
        }
        catch { }
    }
}
