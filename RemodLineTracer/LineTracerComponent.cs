namespace ReModLineTracer
{

    using System.Collections.Generic;

    using ReMod.Core;
    using ReMod.Core.Managers;
    using ReMod.Core.UI.QuickMenu;
    using ReMod.Core.VRChat;

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

        private readonly List<Player> cachedPlayers = new();

        // ReSharper disable once InconsistentNaming
        private ConfigValue<Color> FriendsColor;

        private ConfigValue<bool> lineTracerEnabled;

        private bool materialSetup;

        private Transform originTransform;

        // ReSharper disable once InconsistentNaming
        private ConfigValue<Color> OthersColor;

        private ReMenuToggle tracerToggle;

        public LineTracerComponent()
        {
            lineTracerEnabled = new ConfigValue<bool>(
                nameof(lineTracerEnabled),
                false,
                "Enable Line Tracer (Right Trigger)");
            lineTracerEnabled.OnValueChanged += () => tracerToggle?.Toggle(lineTracerEnabled, false, true);

            FriendsColor = new ConfigValue<Color>(nameof(FriendsColor), Color.yellow);
            OthersColor = new ConfigValue<Color>(nameof(OthersColor), Color.white);
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
            cachedPlayers.Add(player);
        }

        public override void OnPlayerLeft(Player player)
        {
            cachedPlayers.Remove(player);
        }

        public override void OnRenderObject()
        {
            if (!lineTracerEnabled
                || !XRDevice.isPresent) return;

            // In World/Room
            if (!RoomManager.field_Private_Static_Boolean_0) return;

            if (Input.GetAxis(RightTrigger) < 0.4f) return;

            if (!materialSetup) SetupMaterial();

            // local player
            if (!originTransform) originTransform = GetOriginTransform();
            if (originTransform == null) return;

            // Initialize GL
            GL.Begin(1); // Lines
            lineMaterial.SetPass(0);

            // goes way faster to re-use the cached players
            foreach (Player player in cachedPlayers)
            {
                if (!player) continue;
                GL.Color(player.GetAPIUser().isFriend ? FriendsColor.Value : OthersColor.Value);
                GL.Vertex(originTransform.position);
                GL.Vertex(player.transform.position);
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

    }

}