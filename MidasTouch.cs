using Modding;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using HutongGames.PlayMaker;
using SFCore;
using Satchel;

namespace MidasTouch {
    public class MidasTouch: Mod, ILocalSettings<settings> {
        new public string GetName() => "MidasTouch";
        public override string GetVersion() => "1.0.1.1";

        static int frameCount;
        const int drainSpeed = 25;
        const int drainAmount = 3;

        GameObject shinyPrefab;

        internal settings localSettings = new();
        internal static Dictionary<string, EasyCharm> Charms = new() {
            {"MidasTouch", new MidasCharm() },
        };

        public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
            On.PlayMakerFSM.OnEnable += fsmOnEnable;
            On.HealthManager.TakeDamage += takeDamage;
            On.HealthManager.SendDeathEvent += sendDeathEvent;
            On.HeroController.FixedUpdate += heroFixedUpdate;
            On.GameManager.OnNextLevelReady += sceneChange;

            shinyPrefab = preloadedObjects["Fungus1_14"]["Shiny Item"];
        }

        public override List<(string, string)> GetPreloadNames() {
            return new List<(string, string)> {
                ("Fungus1_14", "Shiny Item")
            };
        }

        private void sceneChange(On.GameManager.orig_OnNextLevelReady orig, GameManager self) {
            orig(self);
            if(self.sceneName == "Ruins_House_02") {
                if(PlayerData.instance.killedGorgeousHusk && !Charms["MidasTouch"].GotCharm) {
                    spawnMidasCharmObject(new Vector3(55.5f, 7), false);
                }
                
            }
        }

        private void fsmOnEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
            orig(self);
            if(self.gameObject.name.StartsWith("Shiny Item") && self.FsmName == "Shiny Control") {
                self.GetValidState("Get Charm").InsertAction(new GrantCharm(), 0);
            }
        }

        private void heroFixedUpdate(On.HeroController.orig_FixedUpdate orig, HeroController self) {
            orig(self);
            if(GameManager.instance.isPaused || !Charms["MidasTouch"].IsEquipped)
                return;
            if(++frameCount >= drainSpeed) {
                frameCount = 0;
                if(PlayerData.instance.MPReserve > 0) {
                    HeroController.instance.TakeReserveMP(drainAmount);
                }
                else {
                    HeroController.instance.TakeMPQuick(drainAmount);
                }
            }
        }

        private void sendDeathEvent(On.HealthManager.orig_SendDeathEvent orig, HealthManager self) {
            orig(self);
            if(self.gameObject.name == "Gorgeous Husk") {
                spawnMidasCharmObject(self.gameObject.transform.position, true);
            }
        }

        private void takeDamage(On.HealthManager.orig_TakeDamage orig, HealthManager self, HitInstance hitInstance) {
            if(Charms["MidasTouch"].IsEquipped && hitInstance.AttackType == AttackTypes.Nail) {
                self.gameObject.layer = LayerMask.NameToLayer("Terrain");
                try { self.gameObject.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static; } catch(Exception) { }
                try { self.gameObject.GetComponent<tk2dSpriteAnimator>().enabled = false; } catch(Exception) { }
                try { self.gameObject.GetComponent<InfectedEnemyEffects>().enabled = false; } catch(Exception) { }
                try { self.gameObject.GetComponent<Recoil>().enabled = false; } catch(Exception) { }
                try { self.gameObject.GetComponent<tk2dSprite>().color = new(1, 1, 0); } catch(Exception) { }
                foreach(PlayMakerFSM fsm in self.gameObject.GetComponents<PlayMakerFSM>()) {
                    fsm.enabled = false;
                }
                int health = self.hp;
                int med = Mathf.FloorToInt(health * 3 / 5);
                int small = health * 3 - 5 * med;
                if(health > 1) {
                    self.SetGeoLarge(0);
                    self.SetGeoMedium(med);
                    self.SetGeoSmall(small);
                    HeroController.instance.TakeMPQuick(health);
                    if(health > 99) {
                        HeroController.instance.TakeReserveMP(health - 99);
                    }
                    self.hp = 1;
                }
				if(reflectValue(self, "battleScene") != null && !(bool)reflectValue(self, "notifiedBattleScene")) {
					PlayMakerFSM fsm = FSMUtility.LocateFSM((GameObject)reflectValue(self, "battleScene"), "Battle Control");
					if(fsm != null) {
						FsmInt fsmInt = fsm.FsmVariables.GetFsmInt("Battle Enemies");
						if(fsmInt != null) {
							fsmInt.Value--;
							typeof(HealthManager).GetField("notifiedBattleScene", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(self, true);
						}
					}
				}
                self.enabled = false;
            }
            else {
                orig(self, hitInstance);
            }
        }

        private void spawnMidasCharmObject(Vector3 position, bool flingOnStart) {
            GameObject charm = GameObject.Instantiate(shinyPrefab, position, Quaternion.identity);
            charm.SetActive(true);
            FsmVariables vars = charm.LocateMyFSM("Shiny Control").FsmVariables;
            vars.GetFsmInt("Charm ID").Value = Charms["MidasTouch"].Id;
            vars.GetFsmBool("Fling On Start").Value = flingOnStart;
            vars.GetFsmString("PD Bool Name").Value = "";
        }
        
        private object reflectValue(HealthManager self, string fieldName) {
			return typeof(HealthManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(self);
		}

        public void OnLoadLocal(settings s) {
            localSettings = s;
            if(s.Charms != null) {
                foreach(var kvp in s.Charms) {
                    if(Charms.TryGetValue(kvp.Key, out EasyCharm m)) {
                        m.RestoreCharmState(kvp.Value);
                    }
                }
            }
        }

        public settings OnSaveLocal() {
            localSettings.Charms = new();
            foreach(var kvp in Charms) {
                if(Charms.TryGetValue(kvp.Key, out EasyCharm m)) {
                    localSettings.Charms[kvp.Key] = m.GetCharmState();
                }
            }
            return localSettings;
		}
	}

    public class MidasCharm: EasyCharm {
        protected override int GetCharmCost() => 7;
        protected override string GetDescription() => "Born out of a reckless, soulless desire for wealth and fortune.\r\n\r\nTurns every living being you touch into solid gold at the cost of oneself.\r\n\r\nNot recommended for serious battle.";
        protected override string GetName() => "Midas Touch";
        protected override Sprite GetSpriteInternal() => AssemblyUtils.GetSpriteFromResources("MidasCharm.png");
    }

    public class GrantCharm: FsmStateAction {
        public override void OnEnter() {
            int fsmCharmId = Fsm.GetFsmInt("Charm ID").Value;
            foreach(EasyCharm charm in MidasTouch.Charms.Values) {
                if(charm.Id == fsmCharmId) {
                    charm.GiveCharm(true);
                }
            }
            Finish();
        }
    }

    public class settings {
        public Dictionary<string, EasyCharmState> Charms;
    }
}