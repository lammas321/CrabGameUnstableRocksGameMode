using BepInEx.IL2CPP.Utils;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using UnhollowerRuntimeLib;
using UnityEngine;

namespace UnstableRocksGameMode
{
    public sealed class CustomGameModeUnstableRocks : CustomGameModes.CustomGameMode
    {
        internal Harmony patches;
        internal static bool shouldRocksBreak = false;

        internal static HashSet<UnstableRockStandSlideCheese> rocksCheese;

        public CustomGameModeUnstableRocks() : base
        (
            name: "Unstable Rocks",
            description: "• The rocks are unstable and will break shortly after being stood on\n\n• Stay on your toes and keep moving, but not too fast!",
            gameModeType: GameModeData_GameModeType.FallingPlatforms,
            vanillaGameModeType: GameModeData_GameModeType.FallingPlatforms,
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

            foreach (UnstableRockStandSlideCheese rockCheese in rocksCheese)
                if (rockCheese.inCollider >= 1)
                    rockCheese.Invoke(nameof(rockCheese.PieceFall), 0.75f);
        }

        
        // Change the number of rocks to spawn
        [HarmonyPatch(typeof(GameModeFallingPlatforms), nameof(GameModeFallingPlatforms.InitMode))]
        [HarmonyPostfix]
        internal static void PostInitMode(GameModeFallingPlatforms __instance)
        {
            if (!SteamManager.Instance.IsLobbyOwner())
                return;

            __instance.field_Private_Int32_0 = (int)(35.0f * Mathf.Pow(LobbyManager.Instance.nextRoundPlayers, 0.75f));
        }
        
        // Override, prevent vanilla Floor Is Lava from starting and start Unstable Rocks
        [HarmonyPatch(typeof(GameModeFallingPlatforms), nameof(GameModeFallingPlatforms.OnFreezeOver))]
        [HarmonyPrefix]
        internal static bool PreOnFreezeOver()
        {
            if (!SteamManager.Instance.IsLobbyOwner())
                return false;

            rocksCheese = new(PiecesManager.Instance.pieces.Count);

            foreach (GameObject piece in PiecesManager.Instance.pieces)
            {
                Transform transform = piece.transform;
                GameObject gameObject = new("Unstable Rock");
                gameObject.transform.position = transform.position;
                gameObject.transform.rotation = transform.rotation;
                gameObject.transform.localScale = Vector3.one;

                UnstableRock rock = gameObject.AddComponent<UnstableRock>();
                rock.piece = piece;

                gameObject = new("Unstable Rock Stand Slide Cheese");
                gameObject.transform.position = transform.position;
                gameObject.transform.localRotation = transform.rotation;
                gameObject.transform.localScale = Vector3.one;

                UnstableRockStandSlideCheese rockCheese = gameObject.AddComponent<UnstableRockStandSlideCheese>();
                rockCheese.piece = piece;
                rockCheese.rock = rock;

                rock.rockCheese = rockCheese;

                rocksCheese.Add(rockCheese);
            }

            GameManager.Instance.StartCoroutine(StartDelay());
            return false;
        }
        
        // Make modeTime be 45 seconds
        [HarmonyPatch(typeof(GameModeFallingPlatforms), nameof(GameModeFallingPlatforms.SetPieces))]
        [HarmonyPostfix]
        internal static void PostSetPieces(GameModeFallingPlatforms __instance)
            => __instance.modeTime = 45;
        

        internal class UnstableRock : MonoBehaviour
        {
            internal GameObject piece;
            internal UnstableRockStandSlideCheese rockCheese;

            internal void Awake()
            {
                gameObject.layer = 14; // DetectPlayer layer

                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new(8f, 8f, 2f);
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
                if (!SteamManager.Instance.IsLobbyOwner() || GameManager.Instance.gameMode.modeState != GameMode_ModeState.Playing || !shouldRocksBreak || collider.gameObject.layer != 8 /* Player layer */)
                    return;

                int index = PiecesManager.Instance.pieces.IndexOf(piece);
                if (index != -1)
                    ServerSend.PieceFall(index);

                Destroy(gameObject);
                if (rockCheese)
                    Destroy(rockCheese.gameObject);
            }
        }

        internal class UnstableRockStandSlideCheese : MonoBehaviour
        {
            internal GameObject piece;
            internal UnstableRock rock;
            internal int inCollider = 0;

            internal void Awake()
            {
                gameObject.layer = 14; // DetectPlayer layer

                BoxCollider collider = gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new(8f, 8f, 6f);
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
                if (!SteamManager.Instance.IsLobbyOwner() || collider.gameObject.layer != 8 /* Player layer */)
                    return;

                inCollider++;
                if (inCollider == 1 && GameManager.Instance.gameMode.modeState == GameMode_ModeState.Playing && shouldRocksBreak)
                    Invoke(nameof(PieceFall), 0.75f);
            }

            internal void OnTriggerExit(Collider collider)
            {
                if (!SteamManager.Instance.IsLobbyOwner() || collider.gameObject.layer != 8 /* Player layer */)
                    return;

                inCollider--;
                if (inCollider < 0)
                    inCollider = 0;

                if (inCollider == 0)
                    CancelInvoke(nameof(PieceFall));
            }

            internal void PieceFall()
            {
                int index = PiecesManager.Instance.pieces.IndexOf(piece);
                if (index != -1)
                    ServerSend.PieceFall(index);

                Destroy(gameObject);
                if (rock)
                    Destroy(rock.gameObject);
            }
        }
    }
}