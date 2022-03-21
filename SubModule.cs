using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.AgentOrigins;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.CampaignSystem.Settlements.Workshops;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

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
    }

    public class MyBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.DailyTickSettlementEvent.AddNonSerializedListener(this, DailyTickSettlement);
            CampaignEvents.LocationCharactersAreReadyToSpawnEvent.AddNonSerializedListener(this, SpawnLocationCharacters);
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        }

        private void DailyTickSettlement(Settlement settlement)
        {
            if (settlement.Town == null) return;
            foreach (var w in settlement.Town.Workshops)
            {
                if (w.WorkshopType.StringId == "brewery")
                    InformationManager.DisplayMessage(new InformationMessage(String.Format("{0} has {1}", settlement.Name, w.Name)));
            }
        }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            {
                //string id, string inputToken, string outputToken, string text, ConversationSentence.OnConditionDelegate conditionDelegate, ConversationSentence.OnConsequenceDelegate consequenceDelegate, int priority = 100, ConversationSentence.OnClickableConditionDelegate clickableConditionDelegate = null
                starter.AddDialogLine("artisan_beer_start", "start", "wanna_buy", "Howdy, You gonna buy some beer? One jug is 100 denars.", 
                    () => CharacterObject.OneToOneConversationCharacter == Settlement.CurrentSettlement.Culture.CaravanMaster, null);
                starter.AddPlayerLine("player_buy", "wanna_buy", "thanks", "Sure, I'll buy one", null, () =>
                {
                    Hero.MainHero.ChangeHeroGold(-100);
                    Hero.MainHero.PartyBelongedTo.ItemRoster.AddToCounts(MBObjectManager.Instance.GetObject<ItemObject>("beer"), 1);
                }, 140, (out TextObject explanation) =>
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

        }

        private void SpawnLocationCharacters(Dictionary<string, int> unusedUsablePointCount)
        {
            Location locationWithId = Settlement.CurrentSettlement.LocationComplex.GetLocationWithId("center");
            if (CampaignMission.Current.Location == locationWithId && CampaignTime.Now.IsDayTime)
            {
                Settlement settlement = PlayerEncounter.LocationEncounter.Settlement;
                foreach (Workshop workshop in settlement.Town.Workshops)
                {
                    if (workshop.IsRunning)
                    {
                        int num;
                        unusedUsablePointCount.TryGetValue(workshop.Tag, out num);
                        if (num > 0)
                        {
                            CharacterObject caravanMaster = Settlement.CurrentSettlement.Culture.CaravanMaster;

                            string actionSetCode = "as_human_villager_drinker_with_mug";
                            string value = "artisan_beer_drink_anim";
                            var agentData = new AgentData(new SimpleAgentOrigin(caravanMaster, -1, null, default(UniqueTroopDescriptor))).Monster(Campaign.Current.HumanMonsterSettlement);
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

        public override void SyncData(IDataStore dataStore)
        {
        }
    }
}