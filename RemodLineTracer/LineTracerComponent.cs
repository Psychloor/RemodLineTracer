namespace ReModLineTracer
{
    using ReMod.Core;
    using ReMod.Core.Managers;
    using ReMod.Core.UI.QuickMenu;
    using ReMod.Core.VRChat;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEngine;
    using UnityEngine.XR;
    using VRC;
    using VRC.Core;

    public sealed class LineTracerComponent : ModComponent
    {

        private const string RightTrigger = "Oculus_CrossPlatform_SecondaryIndexTrigger";

        private static readonly int Cull = Shader.PropertyToID("_Cull");

        private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");

        private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");

        private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

        private static Material lineMaterial;

        private readonly List<PlayerInfo> cachedPlayers = new();

        // ReSharper disable once InconsistentNaming
        private readonly ConfigValue<Color> FriendsColor;
        // ReSharper disable once InconsistentNaming
        private readonly ConfigValue<Color> ReModColor;
        // ReSharper disable once InconsistentNaming
        private readonly ConfigValue<Color> OthersColor;

        private Color32 friendsColor, remodColor, othersColor;

        private readonly ConfigValue<bool> lineTracerEnabled;

        private bool materialSetup;

        private Transform originTransform;
        private ReMenuToggle tracerToggle;

        private delegate string GetTrustNameDelegate(APIUser user);
        private static List<GetTrustNameDelegate> getTrustNames;

        public LineTracerComponent()
        {
            lineTracerEnabled = new ConfigValue<bool>(
                nameof(lineTracerEnabled),
                false,
                "Enable Line Tracer (Right Trigger)");
            lineTracerEnabled.OnValueChanged += () => tracerToggle?.Toggle(lineTracerEnabled, false, true);

            FriendsColor = new ConfigValue<Color>(nameof(FriendsColor), Color.yellow);
            ReModColor = new ConfigValue<Color>(nameof(ReModColor), Color.magenta);
            OthersColor = new ConfigValue<Color>(nameof(OthersColor), Color.white);

            FriendsColor.OnValueChanged += () => friendsColor = FriendsColor.Value;
            ReModColor.OnValueChanged += () => remodColor = ReModColor.Value;
            OthersColor.OnValueChanged += () => othersColor = OthersColor.Value;

            friendsColor = FriendsColor.Value;
            remodColor = ReModColor.Value;
            othersColor = OthersColor.Value;

            getTrustNames = new List<GetTrustNameDelegate>();
            var getTrustNameMethods = typeof(VRCPlayer).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => m.Name.StartsWith("Method_Public_Static_String_APIUser_", StringComparison.Ordinal) && m.GetParameters().Length == 1);
            foreach (var method in getTrustNameMethods)
            {
                getTrustNames.Add((GetTrustNameDelegate)Delegate.CreateDelegate(typeof(GetTrustNameDelegate), method));
            }

        }

        public override void OnLeftRoom()
        {
            cachedPlayers.Clear();
        }

        public override void OnEnterWorld(ApiWorld world, ApiWorldInstance instance)
        {
            cachedPlayers.Clear();
        }

        public override void OnPlayerJoined(Player player)
        {
            if (player.GetAPIUser().IsSelf) return;

            var playerInfo = new PlayerInfo
            {
                player = player,
                transform = player.transform,
                isReModUser = IsReModUser(player.GetAPIUser()),
                isFriend = player.GetAPIUser().isFriend
            };

            cachedPlayers.Add(playerInfo);
        }

        private static bool IsReModUser(APIUser apiUser)
        {
            foreach (var method in getTrustNames)
            {
                if (method?.Invoke(apiUser).IndexOf("remod", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return true;
                }
            }

            return false;
        }

        public override void OnPlayerLeft(Player player)
        {
            cachedPlayers.RemoveAll(info => info.player == player);
        }

        public override void OnRenderObject()
        {
            if (!lineTracerEnabled
                || !XRDevice.isPresent) return;

            if (Input.GetAxis(RightTrigger) < 0.4f) return;

            // In World/Room
            if (!RoomManager.field_Private_Static_Boolean_0) return;

            if (!materialSetup) SetupMaterial();

            // local player
            if (!originTransform) originTransform = GetOriginTransform();
            if (originTransform == null) return;

            // Initialize GL
            GL.Begin(1); // Lines
            lineMaterial.SetPass(0);

            // goes way faster to re-use the cached players
            foreach (PlayerInfo info in cachedPlayers)
            {
                if (!info.player) continue;

                if (info.isReModUser)
                    GL.Color(remodColor);
                else
                    GL.Color(info.isFriend ? friendsColor : othersColor);

                GL.Vertex(originTransform.position);
                GL.Vertex(info.transform.position);
            }

            // End GL
            GL.End();
        }

        public override void OnUiManagerInit(UiManager uiManager)
        {
            ReMenuCategory espMenu = uiManager.MainMenu.GetCategoryPage("Visuals").GetCategory("ESP/Highlights");

            tracerToggle = espMenu.AddToggle(
                "[VR] Line Tracer",
                "Hold right trigger to draw lines to each players in world",
                lineTracerEnabled);
        }

        private static Transform GetOriginTransform()
        {
            VRCPlayer localPlayer = VRCPlayer.field_Internal_Static_VRCPlayer_0;
            if (!localPlayer) return null;

            Animator localAnimator = localPlayer.GetAvatarObject()?.GetComponent<Animator>();
            if (localAnimator == null
                || !localAnimator.isHuman) return null;

            // try to grab from the tip of the finger all the way to the hand. otherwise fail
            return localAnimator.GetBoneTransform(HumanBodyBones.RightIndexDistal)
                   ?? localAnimator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate)
                   ?? localAnimator.GetBoneTransform(HumanBodyBones.RightIndexProximal)
                   ?? localAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void SetupMaterial()
        {
            lineMaterial = Material.GetDefaultLineMaterial();
            lineMaterial.SetInt(SrcBlend, 5);
            lineMaterial.SetInt(DstBlend, 10);
            lineMaterial.SetInt(Cull, 0);
            lineMaterial.SetInt(ZWrite, 0);
            lineMaterial.hideFlags = HideFlags.HideAndDontSave;

            materialSetup = true;
        }

        private class PlayerInfo
        {
            public bool isReModUser, isFriend;

            public Player player;

            public Transform transform;
        }

    }

}