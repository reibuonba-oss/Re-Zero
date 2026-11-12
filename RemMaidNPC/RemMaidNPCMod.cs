using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public sealed class RemMaidNPCMod : IMod
{
    private const string ModId = "rem_maid_npc";
    private const string SellerId = "rem_maid_seller";
    private const string PortraitKey = "rem_maid_portrait";
    private const string PhoneDialogId = "RemMaidNPC";
    private const long PhoneNumber = 622202L;
    private const string BusinessCardId = "rem_maid_business_card";

    private static readonly string[] DrinkCandidates =
    {
        "processed_milk", "processed_juice", "bottled_water_premium", "bottled_water",
        "small_bottled_water", "mini_bottle", "large_bottled_water", "water_jug",
        "soda_red", "energy_drink", "galaxy_blend"
    };

    private static readonly string[] FoodCandidates =
    {
        "cat_bar", "li_eat_snackbar", "cup_noodle", "water_ration", "water_ration_small",
        "processed_cheese", "beis_icecream", "bottle_hot_sauce"
    };

    private static readonly string[] LuxuryCandidates =
    {
        "caviar", "skincare_cream", "water_filter_adv", "portable_water_purifier",
        "cassette1", "cassette3", "cassette5", "cassette8", "cassette11"
    };

    private static ModLog _log;
    private static Harmony _harmony;
    private static Texture2D _portraitTexture;
    private static Sprite _portraitSprite;

    public void Init(ModManifest manifest)
    {
        _log = new ModLog(manifest);
        _log.Log("Rem Maid NPC initialized.");
    }

    public void OnEnable()
    {
        StoreClientListDict.storeClientDict[SellerId] = CreateSeller;
        PhoneClientList.DialogFuncs[PhoneDialogId] = CreatePhoneDialog;

        ModHook.OnModAssetsLoaded += OnModAssetsLoaded;
        ModHook.OnModItemDirectoryInit += RegisterItems;
        ModHook.OnGenerateCustomerVeryLate += OnGenerateCustomer;
        ModHook.OnGameLoadedNormal += OnGameLoaded;

        PatchClientNameLocalization();
        _log?.Log("Registered Rem seller, business card, and phone dialogue.");
    }

    public void OnDisable()
    {
        StoreClientListDict.storeClientDict.Remove(SellerId);
        PhoneClientList.DialogFuncs.Remove(PhoneDialogId);

        ModHook.OnModAssetsLoaded -= OnModAssetsLoaded;
        ModHook.OnModItemDirectoryInit -= RegisterItems;
        ModHook.OnGenerateCustomerVeryLate -= OnGenerateCustomer;
        ModHook.OnGameLoadedNormal -= OnGameLoaded;

        _harmony?.UnpatchAll(ModId);
        _harmony = null;
        _portraitSprite = null;
        _portraitTexture = null;
        _log?.Log("Rem Maid NPC disabled.");
    }

    private static string L(string key)
    {
        return ModHelper.GetLocalized(ModId, key);
    }

    private static void PatchClientNameLocalization()
    {
        try
        {
            _harmony = new Harmony(ModId);
            MethodInfo original = AccessTools.Method(typeof(LocHelper), nameof(LocHelper.GetLocalizedClientName), new[] { typeof(string) });
            MethodInfo prefix = AccessTools.Method(typeof(RemMaidNPCMod), nameof(GetRemClientNamePrefix));
            if (original != null && prefix != null)
            {
                _harmony.Patch(original, prefix: new HarmonyMethod(prefix));
            }
            else
            {
                _log?.Warning("Could not patch contact display name; the phone contact may show its localization key.");
            }
        }
        catch (Exception ex)
        {
            _log?.Warning("Phone contact display-name patch failed: " + ex.Message);
        }
    }

    private static bool GetRemClientNamePrefix(string key, ref string __result)
    {
        if (string.Equals(key, "name_rem_maid", StringComparison.Ordinal))
        {
            __result = L("phone.contact_name");
            return false;
        }

        return true;
    }

    private static void OnGameLoaded()
    {
        try
        {
            if (ModHelper.GetModDataInt(ModId, "business_card_received", 0) == 1)
            {
                EnsurePhoneContact();
            }
        }
        catch (Exception ex)
        {
            _log?.Warning("Could not restore Rem's phone contact: " + ex.Message);
        }
    }

    private static void RegisterItems(ModItemDirectory directory)
    {
        ((Directory<GameItem>)(object)directory).Add(BusinessCardId, CreateBusinessCard);
    }

    private static GameItem CreateBusinessCard()
    {
        GameItem item = ItemDirectory.CreateEmptyItem(BusinessCardId);
        item.SetName(L("card.name"));
        item.shortDescription = L("card.description");
        item.flavorText = L("card.flavor");
        item.SetValue(1L);
        item.SetGameItemType("MISC");
        item.SetSpriteAndShapeFromMod(ModId, "rem_business_card");
        return item;
    }

    private static void OnModAssetsLoaded(LoadedMod loadedMod)
    {
        if (loadedMod?.Manifest == null || loadedMod.Manifest.ID != ModId || loadedMod.Textures == null)
        {
            return;
        }

        if (!loadedMod.Textures.TryGetValue("rem", out _portraitTexture) || _portraitTexture == null)
        {
            _log?.Warning(L("log.portrait_missing"));
            return;
        }

        _portraitTexture.filterMode = FilterMode.Point;
        _portraitTexture.wrapMode = TextureWrapMode.Clamp;
        if (!EnsurePortraitRegistered())
        {
            _log?.Warning("Rem portrait will be retried before her next visit.");
        }
        else
        {
            _log?.Log("Rem portrait registered.");
        }
    }

    private static bool EnsurePortraitRegistered()
    {
        if (_portraitTexture == null || SpriteDict.Instance == null || SpriteDict.Instance.spriteDictionary == null)
        {
            return false;
        }

        if (_portraitSprite == null)
        {
            Sprite reference = SpriteDict.Instance.GetSprite("mentor");
            if (reference == null || reference.rect.height <= 0f)
            {
                return false;
            }

            Vector2 normalizedPivot = reference.pivot / reference.rect.size;
            float ppu = reference.pixelsPerUnit * (_portraitTexture.height / reference.rect.height);
            _portraitSprite = Sprite.Create(
                _portraitTexture,
                new Rect(0f, 0f, _portraitTexture.width, _portraitTexture.height),
                normalizedPivot,
                ppu);
            _portraitSprite.name = PortraitKey;
        }

        SpriteDict.Instance.spriteDictionary[PortraitKey] = _portraitSprite;
        return true;
    }

    private static void OnGenerateCustomer(StoreClientManager manager)
    {
        if (manager == null || PlayerStore.Instance == null)
        {
            return;
        }

        try
        {
            if (!EnsurePortraitRegistered())
            {
                _log?.Warning("Skipped Rem's visit because her portrait is not ready.");
                return;
            }

            int day = StoreStation.GetDayCounter();
            if (day < 1 || ModHelper.GetModDataInt(ModId, "last_visit_roll_day", -1) == day)
            {
                return;
            }

            // Only one roll is allowed per in-game day, even if the customer generator runs again.
            ModHelper.SetModDataInt(ModId, "last_visit_roll_day", day);

            int forcedDay = ModHelper.GetModDataInt(ModId, "forced_visit_day", -1);
            bool wasBookedByPhone = forcedDay == day;
            bool shouldVisit = wasBookedByPhone || day == 1 || UnityEngine.Random.Range(0, 100) < 10;

            if (!shouldVisit)
            {
                return;
            }

            bool alreadyQueued = manager.clientStack != null && manager.clientStack.Any(c => c != null && c.identifier == SellerId);
            if (!alreadyQueued)
            {
                manager.AddClient(CreateSeller());
            }

            if (wasBookedByPhone || forcedDay < day)
            {
                ModHelper.SetModDataInt(ModId, "forced_visit_day", -1);
            }

            ModHelper.SetModDataInt(ModId, "last_visit_day", day);
            _log?.Log("Rem visit scheduled for day " + day + (wasBookedByPhone ? " (phone booking)." : "."));
        }
        catch (Exception ex)
        {
            _log?.Error("Rem customer scheduling failed: " + ex);
        }
    }

    private static StoreClient CreateSeller()
    {
        StoreClient client = new StoreClient
        {
            identifier = SellerId,
            displayName = L("npc.name"),
            spriteName = PortraitKey,
            clientFaction = StoreClient.FACTION_TOURIST,
            clientIntent = StoreClient.ClientIntent.SELL,
            reportDegradesFaction = false,
            cooldown = 0
        };

        client.mainDialogue.SetText(client.displayName, L("npc.seller.1"))
            .NextDialogue()
            .SetText(client.displayName, L("npc.seller.2"))
            .NextDialogue()
            .SetText(client.displayName, L("npc.seller.3"))
            .SetEndAction(() =>
            {
                StockRemSeller();
                GrantBusinessCard();
            });

        client.AddBasicDialog();
        client.acceptDealDialogue.SetText(client.displayName, L("npc.accept"));
        client.allDoneDialogue.SetText(client.displayName, L("npc.goodbye"));
        ModHelper.SetClientSource(client, ModId);
        client.CompleteClientCreation(false);
        return client;
    }

    private static void StockRemSeller()
    {
        if (PlayerStore.Instance == null)
        {
            return;
        }

        int count = 0;
        count += AddRandomStock(DrinkCandidates, 2);
        count += AddRandomStock(FoodCandidates, 2);
        count += AddRandomStock(LuxuryCandidates, 2);
        _log?.Log(L("log.stock_added").Replace("{0}", count.ToString()));
    }

    private static int AddRandomStock(string[] candidates, int wantedCount)
    {
        List<string> remaining = new List<string>(candidates);
        int added = 0;
        while (remaining.Count > 0 && added < wantedCount)
        {
            int index = UnityEngine.Random.Range(0, remaining.Count);
            string itemId = remaining[index];
            remaining.RemoveAt(index);

            try
            {
                GameItem item = ItemSpawner.Spawn(itemId);
                if (item == null || item.identifier != itemId || item.unitValue <= 0 || item.unitBaseValue <= 0)
                {
                    _log?.Warning("Rejected Rem stock '" + itemId + "': item is missing or has no valid value.");
                    continue;
                }

                if (PlayerStore.Instance.TryAddDirectSellingItemToTable(item, false, false, false, 0))
                {
                    added++;
                }
                else
                {
                    _log?.Warning("Could not place Rem stock '" + itemId + "' on the counter.");
                }
            }
            catch (Exception ex)
            {
                _log?.Warning("Skipped Rem stock '" + itemId + "': " + ex.Message);
            }
        }

        return added;
    }

    private static void GrantBusinessCard()
    {
        if (PlayerStore.Instance == null || ModHelper.GetModDataInt(ModId, "business_card_received", 0) == 1)
        {
            EnsurePhoneContact();
            return;
        }

        try
        {
            GameItem card = ModHelper.SpawnItem(BusinessCardId);
            if (card == null || card.identifier != BusinessCardId)
            {
                _log?.Warning("Could not create Rem's business card.");
                return;
            }

            ModHelper.MoveItemToInventory(card);
            ModHelper.SetModDataInt(ModId, "business_card_received", 1);
            EnsurePhoneContact();
            _log?.Log(L("log.card_received"));
        }
        catch (Exception ex)
        {
            _log?.Error("Could not grant Rem's business card: " + ex);
        }
    }

    private static void EnsurePhoneContact()
    {
        if (PlayerStore.Instance == null || PlayerStore.Instance.PhoneClientDict == null)
        {
            return;
        }

        if (!PhoneClientList.DialogFuncs.ContainsKey(PhoneDialogId))
        {
            PhoneClientList.DialogFuncs[PhoneDialogId] = CreatePhoneDialog;
        }

        if (PlayerStore.Instance.PhoneClientDict.ContainsKey(PhoneNumber))
        {
            return;
        }

        StorePhoneClient phoneClient = new StorePhoneClient
        {
            displayName = "Rem",
            locID = "name_rem_maid",
            dialogFuncId = PhoneDialogId,
            phoneState = StorePhoneClient.PhoneState.Regular,
            cooldownDuration = 0,
            currentCooldown = 0,
            dialedBefore = false
        };

        PlayerStore.Instance.PhoneClientDict.Add(PhoneNumber, phoneClient);
    }

    private static Dialogue CreatePhoneDialog(StorePhoneClient client)
    {
        Dialogue dialogue = new Dialogue();
        Dialogue booked = new Dialogue();
        booked.SetText(L("phone.title"), L("phone.confirmed"))
            .SetEndAction(() => PhoneUIManager.Instance?.OnPhoneDialogEnd());

        dialogue.SetText(L("phone.title"), L("phone.intro"));
        dialogue.AddChoice(
            DirectoryMaster.Item("$"),
            L("phone.schedule"),
            L("phone.schedule.description"),
            booked,
            () =>
            {
                if (PlayerStore.Instance == null)
                {
                    return;
                }

                int nextDay = StoreStation.GetDayCounter() + 1;
                ModHelper.SetModDataInt(ModId, "forced_visit_day", nextDay);
                PlayerStore.Instance.QueueFuturClient(SellerId, 1);
            });

        return dialogue;
    }
}
