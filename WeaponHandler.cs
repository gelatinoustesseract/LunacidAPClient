using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using LunacidAP.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LunacidAP
{
    public class WeaponHandler
    {
        private static ManualLogSource _log;
        private static string CurrentCastChild { get; set; } = "";
        private static int CurrentElement { get; set; } = 0;

        public WeaponHandler(ManualLogSource log)
        {
            _log = log;
            Harmony.CreateAndPatchAll(typeof(WeaponHandler));
        }

        [HarmonyPatch(typeof(CONTROL), "OnSwap")]
        [HarmonyPostfix]
        private static void OnSwap_ModifyWeaponElement(CONTROL __instance)
        {
            if (__instance.EQ_WEP is null)
            {
                return;
            }
            var weaponName = StaticFuncs.REMOVE_NUMS(__instance.EQ_WEP.name.Replace("(Clone)", ""));
            if (!ArchipelagoClient.AP.SlotData.RandomElements)
            {
                if (ConnectionData.Elements.TryGetValue(weaponName, out string elementName))
                {
                    if (LunacidItems.ElementToID.TryGetValue(elementName, out int element))
                    {
                        __instance.EQ_WEP.WEP_ELEMENT = element;
                    }
                }
            }
            _log.LogInfo(weaponName);
            if (weaponName == "THORN" || weaponName == "GHOST SWORD")
            {
                var currentWeapon = __instance.EQ_WEP.gameObject;
                var oldHand = currentWeapon.transform.Find("HAND");
                GameObject relevantWeapon = (GameObject)Resources.Load("WEPS/MOONLIGHT");
                if (weaponName == "THORN")
                {
                    relevantWeapon = (GameObject)Resources.Load("WEPS/REPLICA SWORD");
                }
                var newHand = GameObject.Instantiate(relevantWeapon.transform.Find("HAND"));
                newHand.parent = oldHand.parent;
                newHand.localPosition = oldHand.localPosition;
                newHand.localRotation = oldHand.localRotation;
                newHand.localScale = oldHand.localScale;
                oldHand.gameObject.SetActive(false);
            }
        }

        [HarmonyPatch(typeof(Break_from_parent), "Start")]
        [HarmonyPostfix]
        private static void Start_ModifyRangedElement(Break_from_parent __instance)
        {
            var castName = __instance.name.Replace("(Clone)", "");
            _log.LogInfo(castName);
            var componentsInChildren = __instance.GetComponentsInChildren(typeof(Damage_Trigger), true);
            if (LunacidItems.CastToWeapon.TryGetValue(castName, out string weapon))
            {
                foreach (Damage_Trigger trigger in componentsInChildren.Cast<Damage_Trigger>())
                {
                    trigger.element = LunacidItems.ElementToID[ConnectionData.Elements[weapon]];
                }
            }
            else
            {
                _log.LogWarning($"Could not change element for {castName}");
            }

        }

        [HarmonyPatch(typeof(Menus), "ItemLoad")]
        [HarmonyPostfix]
        private static void ItemLoad_FixElementInMenu(Menus __instance)
        {
            var eqSelField = __instance.GetType().GetField("EQ_SEL", BindingFlags.Instance | BindingFlags.NonPublic);
            int EQ_SEL = (int)eqSelField.GetValue(__instance);
            int num = EQ_SEL = int.Parse(EventSystem.current.currentSelectedGameObject.name.Substring(3));
            switch (__instance.sub_menu)
            {
                case 4:
                    {
                        if (StaticFuncs.IS_NULL(__instance.CON.CURRENT_PL_DATA.WEPS[num]))
                        {
                            break;
                        }
                        int result2 = -1;
                        GameObject gameObject2;
                        if (int.TryParse(__instance.CON.CURRENT_PL_DATA.WEPS[num].Substring(__instance.CON.CURRENT_PL_DATA.WEPS[num].Length - 2, 2), out result2))
                        {
                            gameObject2 = UnityEngine.Object.Instantiate(Resources.Load("WEPS/" + __instance.CON.CURRENT_PL_DATA.WEPS[num].Substring(0, __instance.CON.CURRENT_PL_DATA.WEPS[num].Length - 2))) as GameObject;
                        }
                        else
                        {
                            gameObject2 = UnityEngine.Object.Instantiate(Resources.Load("WEPS/" + __instance.CON.CURRENT_PL_DATA.WEPS[num])) as GameObject;
                        }
                        Weapon_scr component2 = gameObject2.GetComponent<Weapon_scr>();
                        Sprite elementSprite;
                        var nameWithoutClone = component2.name.Replace("(Clone)", "");
                        if (IsElementShuffled(nameWithoutClone, out var element))
                        {
                            elementSprite = __instance.ELEM[element];
                        }
                        else
                        {
                            elementSprite = __instance.ELEM[component2.WEP_ELEMENT];
                        }
                        __instance.TXT[9].transform.GetChild(0).GetComponent<Image>().sprite = elementSprite;
                        UnityEngine.Object.Destroy(gameObject2);
                        break;
                    }
                case 5:
                    {
                        if (StaticFuncs.IS_NULL(__instance.CON.CURRENT_PL_DATA.SPELLS[num]))
                        {
                            break;
                        }
                        GameObject gameObject = UnityEngine.Object.Instantiate(Resources.Load("MAGIC/" + __instance.CON.CURRENT_PL_DATA.SPELLS[num])) as GameObject;
                        Magic_scr component = gameObject.GetComponent<Magic_scr>();
                        Sprite elementSprite;
                        var nameWithoutClone = component.name.Replace("(Clone)", "");
                        if (nameWithoutClone == "BLOOD STRIKE")
                        {
                            break; // To keep the original flavor of this spell which denotes it as blood
                        }
                        if (IsElementShuffled(nameWithoutClone, out var element))
                        {
                            elementSprite = __instance.ELEM[element];
                        }
                        else
                        {
                            elementSprite = __instance.ELEM[component.MAG_ELEM];
                        }
                        __instance.TXT[22].transform.GetChild(0).GetComponent<Image>().sprite = elementSprite;
                        UnityEngine.Object.Destroy(gameObject);
                        break;
                    }
            }
        }

        private static bool IsElementShuffled(string weaponName, out int element)
        {
            if (ConnectionData.Elements is null)
            {
                element = -1;
                return false;
            }
            if (ConnectionData.Elements.TryGetValue(weaponName, out string givenElement))
            {
                if (LunacidItems.WeaponsWithDefaultElement.Contains(weaponName))
                {
                    element = -1;
                    return false;
                }
                element = LunacidItems.ElementToID[givenElement];
                return true;
            }
            _log.LogError($"The weapon {weaponName} was not in the element dictionary");
            element = -1;
            return false;
        }
    }
}