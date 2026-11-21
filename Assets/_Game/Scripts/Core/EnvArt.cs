using UnityEngine;

namespace BobosJourney
{
    /// <summary>Environment materials, scanned rocks and props, built by Editor/EnvBuilder.cs.</summary>
    public class EnvArt : ScriptableObject
    {
        [Header("Triplanar PBR materials (scanned rock, moss, brick, wood)")]
        public Material island;
        public Material crumble;
        public Material wall;
        public Material wallCheap;   // same stone through mesh UVs, for the huge tower walls
        public Material wood;
        public Material stoneBare;
        public Material emissive;
        public Material cloth;

        [Header("Scanned rock meshes, each with its own material")]
        public Mesh[] rockMeshes;
        public Material[] rockMaterials;

        [Header("Dreamscapes props")]
        public GameObject[] bushes;
        public GameObject[] saplings;
        public GameObject[] mushrooms;
        public GameObject[] pebbles;
        public GameObject[] crystals;
        public GameObject[] pillars;
        public GameObject[] ambientFx;   // spirits, fireflies, dust, drifting fog
        public Material[] grassCards;    // foliage-shader materials from the Dreamscapes grass/flower textures

        static EnvArt instance;

        /// <summary>Loaded from Resources/EnvArt.</summary>
        public static EnvArt Instance
        {
            get
            {
                if (instance == null) instance = Resources.Load<EnvArt>("EnvArt");
                return instance;
            }
        }

        public static T Pick<T>(T[] arr, System.Random rng) => arr == null || arr.Length == 0 ? default : arr[rng.Next(arr.Length)];
    }
}
