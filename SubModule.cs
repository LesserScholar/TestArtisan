using SandBox;
using SandBox.Conversation;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.CampaignSystem.ViewModelCollection;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;
using TaleWorlds.ObjectSystem;
using TaleWorlds.SaveSystem;
using TaleWorlds.ScreenSystem;

namespace TestArtisan
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

        }
        protected override void InitializeGameStarter(Game game, IGameStarter starterObject)
        {
            base.InitializeGameStarter(game, starterObject);
            if (starterObject is CampaignGameStarter starter)
            {
                starter.AddBehavior(new MyBehavior());
            }
        }
        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            mission.AddMissionBehavior(new BeerMissionView());
        }
    }
    public class BeerMissionVM : ViewModel
    {
        ItemObject _beerObject;
        ItemRoster _partyItems;
        int _beerCount;
        Mission _mission;
        public BeerMissionVM(Mission mission)
        {
            _beerObject = MBObjectManager.Instance.GetObject<ItemObject>("artisan_beer");
            _partyItems = MobileParty.MainParty.ItemRoster;
            _beerCount = _partyItems.GetItemNumber(_beerObject);
            _mission = mission;

        }

        [DataSourceProperty]
        public int BeerCount
        {
            get { return _beerCount; }
            set
            {
                if (value != _beerCount)
                {
                    _beerCount = value;
                    OnPropertyChangedWithValue(value, "BeerCount");
                }
            }
        }

        [DataSourceProperty]
        public bool IsVisible => _mission.Mode is MissionMode.Battle or MissionMode.Stealth;

        public void OnMissionModeChange()
        {
            OnPropertyChanged("IsVisible");
        }

        public void DrinkBeer()
        {
            if (BeerCount <= 0) return;
            BeerCount--;
            _partyItems.AddToCounts(_beerObject, -1);
            var agent = Mission.Current.MainAgent;
            var oldHp = agent.Health;
            var newHp = agent.Health + 50;
            if (newHp > agent.HealthLimit) newHp = agent.HealthLimit;
            agent.Health = newHp;
            InformationManager.DisplayMessage(new InformationMessage(String.Format("Healed for {0} hp", newHp - oldHp)));
        }
    }
    public class BeerMissionView : MissionView
    {
        public GauntletLayer layer;
        public BeerMissionVM dataSource;
        public override void OnMissionScreenInitialize()
        {
            layer = new GauntletLayer(0);
            dataSource = new BeerMissionVM(Mission);
            layer.LoadMovie("artisan_beer_combat", dataSource);
            MissionScreen.AddLayer(layer);
        }
        public override void OnMissionScreenFinalize()
        {
            MissionScreen.RemoveLayer(layer);
        }
        public override void OnMissionModeChange(MissionMode oldMissionMode, bool atStart)
        {
            dataSource?.OnMissionModeChange();
        }

        public override void OnMissionScreenTick(float dt)
        {
            if (Mission.Mode is MissionMode.Battle or MissionMode.Stealth)
            {
                if (Input.IsKeyPressed(TaleWorlds.InputSystem.InputKey.Q))
                {
                    dataSource.DrinkBeer();
                }
            }
        }
    }

    public class MyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DailyTickSettlement);
            CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(this, SpawnLocationCharacters);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }
        CharacterObject artisanBrewer;
        ItemObject artisanBeer;

        private void DailyTickSettlement(Settlement settlement)
        {
            if (settlement.Town == null) return;
            foreach (var w in settlement.Town.Workshops)
            {
                if (w.WorkshopType.StringId == "brewery")
                {
                    InformationManager.DisplayMessage(new InformationMessage(String.Format("{0} has {1}", settlement.Name, w.Name)));
                    StorageAddCount(settlement.StringId, w.Tag, 1);
                }
            }
        }
        private Workshop FindCurrentWorkshop(Agent agent)
        {
            if (Settlement.CurrentSettlement != null && Settlement.CurrentSettlement.IsTown)
            {
                CampaignAgentComponent component = agent.GetComponent<CampaignAgentComponent>();
                AgentNavigator agentNavigator = (component != null) ? component.AgentNavigator : null;
                if (agentNavigator != null)
                {
                    foreach (Workshop workshop in Settlement.CurrentSettlement.GetComponent<Town>().Workshops)
                    {
                        if (workshop.Tag == agentNavigator.SpecialTargetTag)
                        {
                            return workshop;
                        }
                    }
                }
            }
            return null;
        }
        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            artisanBrewer = MBObjectManager.Instance.GetObject<CharacterObject>("artisan_brewer");
            artisanBeer = MBObjectManager.Instance.GetObject<ItemObject>("artisan_beer");

            AddDialogs(starter);
            AddGameMenus(starter);

        }

        private void AddDialogs(CampaignGameStarter starter)
        {
            starter.AddDialogLine("artisan_beer_store_empty", "start", "exit", "Howdy, Are you here to buy Beer? Unfortunately we are out of stock. Come back later.",
                () =>
                {
                    if (CharacterObject.OneToOneConversationCharacter != artisanBrewer) return false;
                    var w = FindCurrentWorkshop(ConversationMission.OneToOneConversationAgent);
                    if (w == null || Settlement.CurrentSettlement == null) return false;
                    return BeerInStorage(Settlement.CurrentSettlement.StringId, w.Tag) <= 0;
                }, null);
            starter.AddDialogLine("artisan_beer_start", "start", "wanna_buy", "Howdy, You gonna buy some beer? One jug is 100 denars.",
                () =>
                {
                    if (CharacterObject.OneToOneConversationCharacter != artisanBrewer) return false;
                    var w = FindCurrentWorkshop(ConversationMission.OneToOneConversationAgent);
                    if (w == null || Settlement.CurrentSettlement == null) return false;
                    return BeerInStorage(Settlement.CurrentSettlement.StringId, w.Tag) > 0;
                }, null);
            starter.AddPlayerLine("player_buy", "wanna_buy", "thanks", "Sure, I'll buy one", null, () =>
            {
                Hero.MainHero.ChangeHeroGold(-100);
                Hero.MainHero.PartyBelongedTo.ItemRoster.AddToCounts(artisanBeer, 1);

                var w = FindCurrentWorkshop(ConversationMission.OneToOneConversationAgent);
                StorageAddCount(Settlement.CurrentSettlement.StringId, w.Tag, -1);
            }, 100, (out TextObject explanation) =>
            {
                if (Hero.MainHero.Gold < 100)
                {
                    explanation = new TextObject("Not enough money");
                    return false;
                }
                explanation = TextObject.Empty;
                return true;
            });
            starter.AddPlayerLine("player_nah", "wanna_buy", "your_loss", "Nah, I'll pass", null, null);

            starter.AddDialogLine("your_loss", "your_loss", "exit", "Your loss", null, null);
            starter.AddDialogLine("thanks", "thanks", "exit", "Thank you come again", null, null);
        }
        Workshop FirstPlayerLocalBrewery()
        {
            if (Settlement.CurrentSettlement == null) return null;
            foreach (var w in Settlement.CurrentSettlement.Town.Workshops)
            {
                if (w.Owner == Hero.MainHero && w.WorkshopType.StringId == "brewery") return w;
            }
            return null;
        }
        private void AddGameMenus(CampaignGameStarter starter)
        {
            starter.AddGameMenuOption("town", "town_brewery", "Manage Brewery", (args) =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
                return FirstPlayerLocalBrewery() != null;
            }, (args) => { GameMenu.SwitchToMenu("town_brewery"); }, false, 9);
            starter.AddGameMenu("town_brewery", "Brewery", (args) => { }, TaleWorlds.CampaignSystem.Overlay.GameOverlays.MenuOverlayType.SettlementWithCharacters);
            starter.AddGameMenuOption("town_brewery", "town_brewery_inventory", "Access brewery storage", Submenu, (args) => {
                List<InquiryElement> list = new List<InquiryElement>();
                var brewery = FirstPlayerLocalBrewery();
                if (brewery != null) {
                    for (int i = 0; i < BeerInStorage(Settlement.CurrentSettlement.StringId, brewery.Tag); i++)
                    {
                        list.Add(new InquiryElement(artisanBeer, "Artisan Beer", new ImageIdentifier(artisanBeer)));
                    }
                }
                InformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Inventory", "Take item", list, true, 5, "Take", new TextObject("{=3CpNUnVl}Cancel", null).ToString(), TakeFromStorage, (args) => { }));
            });
            starter.AddGameMenuOption("town_brewery", "town_brewery_manage", "Manage Production", Submenu, (args) => { });
            starter.AddGameMenuOption("town_brewery", "town_brewery_back", "{=qWAmxyYz}Back to town center", (args) => { args.optionLeaveType = GameMenuOption.LeaveType.Leave; return true; },
                (args) => GameMenu.SwitchToMenu("town"));
        }

        private void TakeFromStorage(List<InquiryElement> list)
        {
            var brewery = FirstPlayerLocalBrewery();
            if (brewery != null)
            {
                StorageAddCount(Settlement.CurrentSettlement.StringId, brewery.Tag, -list.Count);
                MobileParty.MainParty.ItemRoster.AddToCounts(artisanBeer, list.Count);
            }
        }

        private static bool Submenu(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
            return true;
        }

        private void SpawnLocationCharacters(Dictionary<string, int> unusedUsablePointCount)
        {
            Location locationWithId = Settlement.CurrentSettlement.LocationComplex.GetLocationWithId("center");
            if (CampaignMission.Current.Location == locationWithId && CampaignTime.Now.IsDayTime)
            {
                Settlement settlement = PlayerEncounter.LocationEncounter.Settlement;
                foreach (Workshop workshop in settlement.Town.Workshops)
                {
                    if (workshop.WorkshopType.StringId == "brewery")
                    {
                        int num;
                        unusedUsablePointCount.TryGetValue(workshop.Tag, out num);
                        if (num > 0)
                        {
                            string actionSetCode = "as_human_villager_drinker_with_mug";
                            string value = "artisan_beer_drink_anim";
                            var agentData = new AgentData(new SimpleAgentOrigin(artisanBrewer, -1, null, default(UniqueTroopDescriptor))).Monster(Campaign.Current.HumanMonsterSettlement);
                            LocationCharacter locationCharacter = new LocationCharacter(
                                agentData,
                                new LocationCharacter.AddBehaviorsDelegate(SandBoxManager.Instance.AgentBehaviorManager.AddWandererBehaviors),
                                workshop.Tag, true, LocationCharacter.CharacterRelations.Friendly, actionSetCode, true, false, null, false, false, true)
                            {
                                PrefabNamesForBones =
                                {
                                    {
                                        agentData.AgentMonster.MainHandItemBoneIndex,
                                        value
                                    }
                                }
                            };

                            locationWithId.AddCharacter(locationCharacter);
                        }
                    }
                }

            }
        }
        int BeerInStorage(string s, string w)
        {
            int value;
            if (beer_storage.TryGetValue(s + '_' + w, out value)) return value;
            return 0;
        }
        void StorageAddCount(string s, string w, int count)
        {
            int value;
            if (!beer_storage.TryGetValue(s + '_' + w, out value)) value = 0;

            value += count;

            if (value < 0) throw new ArgumentException("Trying to pull nonexistent artisan beer from storage");
            if (value > 5) value = 5;

            beer_storage[s + '_' + w] = value;
        }

        Dictionary<string, int> beer_storage = new Dictionary<string, int>();

        public override void SyncData(IDataStore dataStore)
        {
            dataStore.SyncData("beer_storage", ref beer_storage);
        }
    }
    public class TypeDefiner : SaveableTypeDefiner
    {
        public TypeDefiner() : base(423_324_859)
        {
        }
        protected override void DefineContainerDefinitions()
        {
        }
    }
}