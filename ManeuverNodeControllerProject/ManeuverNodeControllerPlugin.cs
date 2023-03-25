﻿using BepInEx;
using UnityEngine;
using KSP.Game;
using KSP.Sim.impl;
using KSP.Sim.Maneuver;
using SpaceWarp;
using SpaceWarp.API;
using SpaceWarp.API.Mods;
using SpaceWarp.API.Assets;
using SpaceWarp.API.UI;
using SpaceWarp.API.UI.Appbar;
using KSP.UI.Binding;
using MoonSharp.Interpreter.Tree;
using KSP.Messages.PropertyWatchers;
using Unity.Collections.LowLevel.Unsafe;

namespace ManeuverNodeController
{
    [BepInDependency(SpaceWarpPlugin.ModGuid, SpaceWarpPlugin.ModVer)]
    [BepInPlugin("com.github.xyz3211.maneuvernodecontroller", "Maneuver Node Controller", "0.6.0")]
    public class ManeuverNodeControllerMod : BaseSpaceWarpPlugin
    {
        private static ManeuverNodeControllerMod Instance { get; set; }

        static bool loaded = false;
        private bool interfaceEnabled = false;
        private Rect windowRect;
        private int windowWidth = Screen.width / 5; //384px on 1920x1080
        private int windowHeight = Screen.height / 3; //360px on 1920x1080

        private string progradeString = "0";
        private string normalString = "0";
        private string radialString = "0";
        private string absoluteValueString = "0";
        private string smallStepString = "5";
        private string bigStepString = "25";
        private string timeSmallStepString = "5";
        private string timeLargeStepString = "25";
        private double absoluteValue, smallStep, bigStep, timeSmallStep, timeLargeStep;
        private VesselComponent activeVessel;

        private bool pAbs, pInc1, pInc2, pDec1, pDec2, nAbs, nInc1, nInc2, nDec1, nDec2, rAbs, rInc1, rInc2, rDec1, rDec2, timeInc1, timeInc2, timeDec1, timeDec2, orbitInc, orbitDec, addNode;
        private bool advancedMode;

        // SnapTo selection.
        private enum SnapOptions
        {
            Apoapsis,
            Periapsis
        }

        private SnapOptions selectedSnapOption = SnapOptions.Apoapsis;
        //private readonly List<string> snapOptions = new List<string> { "Apoapsis", "Periapsis" };
        private bool selectingSnapOption = false;
        private static Vector2 scrollPositionSnapOptions;
        private bool applySnapOption;

        private ManeuverNodeData currentNode = null;
        List<ManeuverNodeData> activeNodes;
        private Vector3d burnParams;

        private GUIStyle errorStyle, warnStyle, progradeStyle, normalStyle, radialStyle, labelStyle;
        private GameInstance game;
        private GUIStyle horizontalDivider = new GUIStyle();

        public override void OnInitialized()
        {
            Logger.LogInfo("Loaded");
            if (loaded)
            {
                Destroy(this);
            }
            loaded = true;

            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);

            Appbar.RegisterAppButton(
                "Maneuver Node Cont.",
                "BTN-ManeuverNodeController",
                AssetManager.GetAsset<Texture2D>($"{SpaceWarpMetadata.ModID}/images/icon.png"),
                ToggleButton);
        }

        private void ToggleButton(bool toggle)
        {
            interfaceEnabled = toggle;
            GameObject.Find("BTN-ManeuverNodeController")?.GetComponent<UIValue_WriteBool_Toggle>()?.SetValue(toggle);
        }

        void Awake()
        {
            windowRect = new Rect((Screen.width * 0.7f) - (windowWidth / 2), (Screen.height / 2) - (windowHeight / 2), 0, 0);
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.N))
            {
                ToggleButton(!interfaceEnabled);
                Logger.LogInfo("UI toggled with hotkey");
            }
        }

        void OnGUI()
        {
            if (interfaceEnabled)
            {
                GUI.skin = Skins.ConsoleSkin;

                windowRect = GUILayout.Window(
                    GUIUtility.GetControlID(FocusType.Passive),
                    windowRect,
                    FillWindow,
                    "<color=#696DFF>// MANEUVER NODE CONTROLLER</color>",
                    GUILayout.Height(windowHeight),
                    GUILayout.Width(windowWidth));
            }

        }

        private void FillWindow(int windowID)
        {
            labelStyle = warnStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            errorStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            errorStyle.normal.textColor = Color.red;
            warnStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            warnStyle.normal.textColor = Color.yellow;
            progradeStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            progradeStyle.normal.textColor = Color.yellow;
            normalStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            normalStyle.normal.textColor = Color.magenta;
            radialStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            radialStyle.normal.textColor = Color.cyan;
            horizontalDivider.fixedHeight = 2;
            horizontalDivider.margin = new RectOffset(0, 0, 4, 4);

            game = GameManager.Instance.Game;
            activeNodes = game.SpaceSimulation.Maneuvers.GetNodesForVessel(GameManager.Instance.Game.ViewController.GetActiveVehicle(true).Guid);
            currentNode = (activeNodes.Count() > 0) ? activeNodes[0] : null;

            GUILayout.BeginVertical();

            if (currentNode == null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("The active vessel has no maneuver nodes.", errorStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                addNode = GUILayout.Button("Add Node", GUILayout.Width(windowWidth / 4));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                handleButtons();
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"Total Maneuver dV (m/s): ");
                GUILayout.FlexibleSpace();
                GUILayout.Label(currentNode.BurnRequiredDV.ToString("n2"));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Prograde dV (m/s): ");
                GUILayout.FlexibleSpace();
                GUILayout.Label(currentNode.BurnVector.z.ToString("n2"));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Normal dV (m/s): ");
                GUILayout.FlexibleSpace();
                GUILayout.Label(currentNode.BurnVector.y.ToString("n2"));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label($"Radial dV (m/s): ");
                GUILayout.FlexibleSpace();
                GUILayout.Label(currentNode.BurnVector.x.ToString("n2"));
                GUILayout.EndHorizontal();

                GUILayout.Box("", horizontalDivider);

                if (advancedMode)
                {
                    //advancedMode not yet enabled
                    drawAdvancedMode();
                }
                else
                {
                    drawSimpleMode();
                }

            }

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 500));
        }

        private void drawAdvancedMode()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("Prograde dV (m/s): ", GUILayout.Width(windowWidth / 2));
            progradeString = GUILayout.TextField(progradeString, progradeStyle);
            double.TryParse(progradeString, out burnParams.z);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Normal dV (m/s): ", GUILayout.Width(windowWidth / 2));
            normalString = GUILayout.TextField(normalString, normalStyle);
            double.TryParse(normalString, out burnParams.y);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Radial dV (m/s): ", GUILayout.Width(windowWidth / 2));
            radialString = GUILayout.TextField(radialString, radialStyle);
            double.TryParse(radialString, out burnParams.x);
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Apply Changes to Node"))
            {
                ManeuverNodeData nodeData = GameManager.Instance.Game.SpaceSimulation.Maneuvers.GetNodesForVessel(GameManager.Instance.Game.ViewController.GetActiveVehicle(true).Guid)[0];
                //nodeData.BurnVector = burnParams;
                game.UniverseModel.FindVesselComponent(nodeData.RelatedSimID)?.SimulationObject.FindComponent<ManeuverPlanComponent>().UpdateChangeOnNode(nodeData, burnParams);
                game.UniverseModel.FindVesselComponent(nodeData.RelatedSimID)?.SimulationObject.FindComponent<ManeuverPlanComponent>().RefreshManeuverNodeState(0);
                Logger.LogInfo(nodeData.ToString());
            }
        }

        private void drawSimpleMode()
        {


            GUILayout.BeginHorizontal();
            GUILayout.Label("Absolute dV (m/s): ", GUILayout.Width(windowWidth / 2));
            absoluteValueString = GUILayout.TextField(absoluteValueString);
            double.TryParse(absoluteValueString, out absoluteValue);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Small Step dV (m/s): ", GUILayout.Width(windowWidth / 2));
            smallStepString = GUILayout.TextField(smallStepString);
            double.TryParse(smallStepString, out smallStep);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Large Step dV (m/s): ", GUILayout.Width(windowWidth / 2));
            bigStepString = GUILayout.TextField(bigStepString);
            double.TryParse(bigStepString, out bigStep);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            pDec2 = GUILayout.Button("<<", GUILayout.Width(windowWidth / 9));
            pDec1 = GUILayout.Button("<", GUILayout.Width(windowWidth / 9));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Prograde", progradeStyle);
            GUILayout.FlexibleSpace();
            pInc1 = GUILayout.Button(">", GUILayout.Width(windowWidth / 9));
            pInc2 = GUILayout.Button(">>", GUILayout.Width(windowWidth / 9));
            pAbs = GUILayout.Button("Abs", GUILayout.Width(windowWidth / 9));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            nDec2 = GUILayout.Button("<<", GUILayout.Width(windowWidth / 9));
            nDec1 = GUILayout.Button("<", GUILayout.Width(windowWidth / 9));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Normal", normalStyle);
            GUILayout.FlexibleSpace();
            nInc1 = GUILayout.Button(">", GUILayout.Width(windowWidth / 9));
            nInc2 = GUILayout.Button(">>", GUILayout.Width(windowWidth / 9));
            nAbs = GUILayout.Button("Abs", GUILayout.Width(windowWidth / 9));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            rDec2 = GUILayout.Button("<<", GUILayout.Width(windowWidth / 9));
            rDec1 = GUILayout.Button("<", GUILayout.Width(windowWidth / 9));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Radial", radialStyle);
            GUILayout.FlexibleSpace();
            rInc1 = GUILayout.Button(">", GUILayout.Width(windowWidth / 9));
            rInc2 = GUILayout.Button(">>", GUILayout.Width(windowWidth / 9));
            rAbs = GUILayout.Button("Abs", GUILayout.Width(windowWidth / 9));
            GUILayout.EndHorizontal();

            GUILayout.Box("", horizontalDivider);

            SnapSelectionGUI();

            GUILayout.Box("", horizontalDivider);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Small Time Step (seconds): ", GUILayout.Width(2*windowWidth / 3));
            timeSmallStepString = GUILayout.TextField(timeSmallStepString);
            double.TryParse(timeSmallStepString, out timeSmallStep);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Large Time Step (seconds): ", GUILayout.Width(2*windowWidth / 3));
            timeLargeStepString = GUILayout.TextField(timeLargeStepString);
            double.TryParse(timeLargeStepString, out timeLargeStep);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            timeDec2 = GUILayout.Button("<<", GUILayout.Width(windowWidth / 9));
            timeDec1 = GUILayout.Button("<", GUILayout.Width(windowWidth / 9));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Time", labelStyle);
            GUILayout.FlexibleSpace();
            timeInc1 = GUILayout.Button(">", GUILayout.Width(windowWidth / 9));
            timeInc2 = GUILayout.Button(">>", GUILayout.Width(windowWidth / 9));
            GUILayout.EndHorizontal();

            GUILayout.Box("", horizontalDivider);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Maneuver Node in: ");
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{Math.Truncate((currentNode.Time - game.UniverseModel.UniversalTime) / game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID).Orbit.period).ToString("n0")} orbit(s) ");
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            orbitDec = GUILayout.Button("-", GUILayout.Width(windowWidth / 7));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Orbit", labelStyle);
            GUILayout.FlexibleSpace();
            orbitInc = GUILayout.Button("+", GUILayout.Width(windowWidth / 7));
            GUILayout.EndHorizontal();

            handleButtons();
        }

        // Draws the snap selection GUI.
        private void SnapSelectionGUI()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("SnapTo: ", GUILayout.Width(windowWidth / 5));
            if (!selectingSnapOption)
            {
            if (GUILayout.Button(Enum.GetName(typeof(SnapOptions), selectedSnapOption)))
                selectingSnapOption = true;
            }
            else
            {
            GUILayout.BeginVertical(GUI.skin.GetStyle("Box"));
            scrollPositionSnapOptions = GUILayout.BeginScrollView(scrollPositionSnapOptions, false, true, GUILayout.Height(70));

            foreach (string snapOption in Enum.GetNames(typeof(SnapOptions)).ToList())
            {
                if (GUILayout.Button(snapOption))
                {
                    Enum.TryParse(snapOption, out selectedSnapOption);
                    selectingSnapOption = false;
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            }

            applySnapOption = GUILayout.Button("Snap", GUILayout.Width(windowWidth / 5));
            GUILayout.EndHorizontal();
        }

        private void handleButtons()
        {
            if (addNode)
            {
                // Add an empty maneuver node
                Logger.LogInfo("Adding New Node");
                activeVessel = GameManager.Instance?.Game?.ViewController?.GetActiveVehicle(true)?.GetSimVessel(true);
                Logger.LogInfo($"Vessel: {activeVessel.Name}");
                Logger.LogInfo($"Vessel ID: {activeVessel.SimulationObject.GlobalId}");
                Logger.LogInfo($"UT: {game.UniverseModel.UniversalTime}");
                // Need a KSP.Sim.impl.IGGuid for the first argument to ManeuverNodeData
                ManeuverNodeData nodeData = new ManeuverNodeData(activeVessel.SimulationObject.GlobalId, false, game.UniverseModel.UniversalTime);
                Logger.LogInfo($"Node Data: {nodeData.ToString()}");
                nodeData.BurnVector.x = 0;
                nodeData.BurnVector.y = 0;
                nodeData.BurnVector.z = 0;
                Logger.LogInfo($"Node Data with Zero Burn: {nodeData.ToString()}");
                currentNode = nodeData;
                nodeData.Time = game.UniverseModel.UniversalTime + game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID).Orbit.TimeToAp;
                Logger.LogInfo($"Node Data with Time set to Ap: {nodeData.ToString()}");
                // Logger.LogInfo(nodeData.ToString());
                GameManager.Instance.Game.SpaceSimulation.Maneuvers.AddNodeToVessel(nodeData);
                // game.UniverseModel.FindVesselComponent(nodeData.RelatedSimID)?.SimulationObject.FindComponent<ManeuverPlanComponent>().UpdateChangeOnNode(nodeData, burnParams);
                game.UniverseModel.FindVesselComponent(nodeData.RelatedSimID)?.SimulationObject.FindComponent<ManeuverPlanComponent>().RefreshManeuverNodeState(0);
                addNode = false;
            }

            if (currentNode == null)
            {
                return;
            }

            if (pAbs || pInc1 || pInc2 || pDec1 || pDec2 || nAbs || nInc1 || nInc2 || nDec1 || nDec2 || rAbs || rInc1 || rInc2 || rDec1 || rDec2 || timeDec1 || timeDec2 || timeInc1 || timeInc2 || orbitDec || orbitInc || applySnapOption || addNode)
            {
                burnParams = Vector3d.zero;

                if(pAbs)
                {
                    currentNode.BurnVector.z = absoluteValue;
                }
                else if (pInc1)
                {
                    burnParams.z += smallStep;
                }
                else if (pInc2)
                {
                    burnParams.z += bigStep;
                }
                else if (nAbs)
                {
                    currentNode.BurnVector.y = absoluteValue;
                }
                else if (nInc1)
                {
                    burnParams.y += smallStep;
                }
                else if (nInc2)
                {
                    burnParams.y += bigStep;
                }
                else if (rAbs)
                {
                    currentNode.BurnVector.x = absoluteValue;
                }
                else if (rInc1)
                {
                    burnParams.x += smallStep;
                }
                else if (rInc2)
                {
                    burnParams.x += bigStep;
                }
                else if (pDec1)
                {
                    burnParams.z -= smallStep;
                }
                else if (pDec2)
                {
                    burnParams.z -= bigStep;
                }
                else if (nDec1)
                {
                    burnParams.y -= smallStep;
                }
                else if (nDec2)
                {
                    burnParams.y -= bigStep;
                }
                else if (rDec1)
                {
                    burnParams.x -= smallStep;
                }
                else if (rDec2)
                {
                    burnParams.x -= bigStep;
                }
                else if (timeDec1)
                {
                    currentNode.Time -= timeSmallStep;
                }
                else if (timeDec2)
                {
                    currentNode.Time -= timeLargeStep;
                }
                else if (timeInc1)
                {
                    currentNode.Time += timeSmallStep;
                }
                else if (timeInc2)
                {
                    currentNode.Time += timeLargeStep;
                }
                else if (orbitDec)
                {
                    if (game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID).Orbit.period < (currentNode.Time- game.UniverseModel.UniversalTime))
                    {
                        currentNode.Time -= game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID).Orbit.period;
                    }
                }
                else if (orbitInc)
                {
                    currentNode.Time += game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID).Orbit.period;
                }
                else if(applySnapOption)    //apply selected snap option
                {
                    if (selectedSnapOption == SnapOptions.Apoapsis)
                        currentNode.Time = game.UniverseModel.UniversalTime + game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID).Orbit.TimeToAp;
                    else if (selectedSnapOption == SnapOptions.Periapsis)
                        currentNode.Time = game.UniverseModel.UniversalTime + game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID).Orbit.TimeToPe;
                }

                game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID)?.SimulationObject.FindComponent<ManeuverPlanComponent>().UpdateChangeOnNode(currentNode, burnParams);
                game.UniverseModel.FindVesselComponent(currentNode.RelatedSimID)?.SimulationObject.FindComponent<ManeuverPlanComponent>().RefreshManeuverNodeState(0);
            }
        }
    }   
}