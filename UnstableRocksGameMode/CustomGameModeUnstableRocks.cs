using BepInEx.IL2CPP.Utils;
using HarmonyLib;
using System;
using System.Collections;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace UnstableRocksGameMode
{
    public sealed class CustomGameModeUnstableRocks : CustomGameModes.CustomGameMode
    {
        internal Harmony patches;
        internal static bool shouldRocksBreak = false;

        public CustomGameModeUnstableRocks() : base
        (
            name: "Unstable Rocks",
            description: "• The rocks are unstable and will break shortly after being stood on\n\n• Stay on your toes and keep moving, but not too fast!",
            gameModeType: GameModeType.FallingPlatforms,
            vanillaGameModeType: GameModeType.FallingPlatforms,
            waitForRoundOverToDeclareSoloWinner: true,

            shortModeTime: 40,
            mediumModeTime: 40,
            longModeTime: 40,

            compatibleMapNames: [
                "Crusty Rocks",
                "Icy Rocks",
                "Lava Lake",
                "Sandy Stones"
            ]
        )
        {
            ClassInjector.RegisterTypeInIl2Cpp<UnstableRock>();
            ClassInjector.RegisterTypeInIl2Cpp<UnstableRockStandSlideCheese>();
        }

        public override void PreInit()
        {
            shouldRocksBreak = false;
            patches = Harmony.CreateAndPatchAll(GetType());
        }
        public override void PostEnd()
        {
            shouldRocksBreak = false;
            patches?.UnpatchSelf();
        }

        public static IEnumerator StartDelay()
        {
            yield return new WaitForSeconds(2);
            shouldRocksBreak = true;
        }

        
        // Change the number of rocks to spawn
        [HarmonyPatch(typeof(GameModeFloorIsLava), nameof(GameModeFloorIsLava.InitMode))]
        [HarmonyPostfix]
        internal static void PostInitMode(GameModeFloorIsLava __instance)
        {
            if (!SteamManager.Instance.IsLobbyOwner())
                return;

            __instance.field_Private_Int32_0 = (int)(35.0 * Math.Pow(LobbyManager.Instance.nextRoundPlayers, 0.75));
        }
        
        // Override, prevent vanilla Floor Is Lava from starting and start Unstable Rocks
        [HarmonyPatch(typeof(GameModeFloorIsLava), nameof(GameModeFloorIsLava.OnFreezeOver))]
        [HarmonyPrefix]
        internal static bool PreOnFreezeOver()
        {
            if (!SteamManager.Instance.IsLobbyOwner())
                return false;

            foreach (GameObject piece in FloorIsLavaPieceManager.Instance.pieces)
            {
                Transform transform = piece.transform;
                GameObject gameObject = new("Unstable Rock");
                gameObject.transform.position = transform.position;
                gameObject.transform.rotation = transform.rotation;
                gameObject.transform.localScale = Vector3.one;
                gameObject.AddComponent<UnstableRock>().piece = piece;

                gameObject = new("Unstable Rock Stand Slide Cheese");
                gameObject.transform.position = transform.position;
                gameObject.transform.localRotation = transform.rotation;
                gameObject.transform.localScale = Vector3.one;
                gameObject.AddComponent<UnstableRockStandSlideCheese>().piece = piece;
            }

            GameManager.Instance.StartCoroutine(StartDelay());
            return false;
        }
        
        // Make modeTime be 45 seconds
        [HarmonyPatch(typeof(GameModeFloorIsLava), nameof(GameModeFloorIsLava.SetPieces))]
        [HarmonyPostfix]
        internal static void PostSetPieces(GameModeFloorIsLava __instance)
            => __instance.modeTime = 45;
        

        internal class UnstableRock : MonoBehaviour
        {
            internal GameObject piece;

            internal void Start()
            {
                gameObject.layer = 14; // DetectPlayer layer

                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new(8, 8, 2);
                collider.center = new(-0.3f, -0.1f, 0.25f);

                // Visualize collider
                /*GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh meshCube = cube.GetComponent<MeshFilter>().sharedMesh;
                Destroy(cube);

                GameObject colVisObj = new("Collider Visualization");
                colVisObj.transform.parent = transform;
                colVisObj.transform.localPosition = collider.center;
                colVisObj.transform.localRotation = Quaternion.identity;
                colVisObj.transform.localScale = collider.size;
                MeshRenderer renderer = colVisObj.AddComponent<MeshRenderer>();
                renderer.enabled = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                colVisObj.AddComponent<MeshFilter>().mesh = meshCube;*/
            }

            internal void OnTriggerStay(Collider collider)
            {
                if (!SteamManager.Instance.IsLobbyOwner() || GameManager.Instance.gameMode.modeState != GameModeState.Playing || !CustomGameModeUnstableRocks.shouldRocksBreak || collider.gameObject.layer != 8 /* Player layer */) return;

                int index = FloorIsLavaPieceManager.Instance.pieces.IndexOf(piece);
                if (index != -1)
                    ServerSend.PieceFall(index);
                Destroy(transform);
            }
        }

        internal class UnstableRockStandSlideCheese : MonoBehaviour
        {
            internal GameObject piece;
            internal int inCollider = 0;
            internal float timeInCollider = 0;

            internal void Start()
            {
                gameObject.layer = 14; // DetectPlayer layer

                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new(8, 8, 6);
                collider.center = new(-0.3f, -0.1f, 4.25f);

                // Visualize collider
                /*GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Mesh meshCube = cube.GetComponent<MeshFilter>().sharedMesh;
                Destroy(cube);

                GameObject colVisObj = new("Collider Visualization");
                colVisObj.transform.parent = transform;
                colVisObj.transform.localPosition = collider.center;
                colVisObj.transform.localRotation = Quaternion.identity;
                colVisObj.transform.localScale = collider.size;
                MeshRenderer renderer = colVisObj.AddComponent<MeshRenderer>();
                renderer.enabled = true;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                colVisObj.AddComponent<MeshFilter>().mesh = meshCube;*/
            }

            internal void OnTriggerEnter(Collider collider)
            {
                if (SteamManager.Instance.IsLobbyOwner() && collider.gameObject.layer == 8 /* Player layer */)
                    inCollider++;
            }

            internal void OnTriggerExit(Collider collider)
            {
                if (SteamManager.Instance.IsLobbyOwner() && collider.gameObject.layer == 8 /* Player layer */)
                    inCollider--;
            }

            internal void FixedUpdate()
            {
                if (!SteamManager.Instance.IsLobbyOwner() || GameManager.Instance.gameMode.modeState != GameModeState.Playing || !CustomGameModeUnstableRocks.shouldRocksBreak) return;

                if (inCollider < 0)
                    inCollider = 0;
                if (inCollider == 0)
                {
                    timeInCollider = 0;
                    return;
                }

                timeInCollider += Time.fixedDeltaTime;
                if (timeInCollider < 0.75f)
                    return;

                int index = FloorIsLavaPieceManager.Instance.pieces.IndexOf(piece);
                if (index != -1)
                    ServerSend.PieceFall(index);
                Destroy(transform);
            }
        }
    }
}