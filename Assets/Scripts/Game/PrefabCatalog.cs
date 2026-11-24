using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepft.Game
{
    [Serializable]
    public sealed class NamedPrefab
    {
        /// <summary>스프라이트 파일 이름과 같다. 예: `sheep`, `stone`, `splash`</summary>
        public string name;
        public GameObject prefab;
    }

    /// <summary>
    /// 게임이 찍어 내는 프리팹의 목록. 맵은 `stages.txt`를 읽어 이 프리팹들로 만든다.
    /// 메뉴 Sheepft > Setup Game Scene이 비어 있는 칸만 채운다. 다른 프리팹으로 바꿔 끼워도 된다.
    /// </summary>
    public sealed class PrefabCatalog : MonoBehaviour
    {
        [Tooltip("오브젝트: sheep, wolf, goat, horse, cow, fruit_basket, wooden_bucket, spicebush, buff_sheep")]
        public List<NamedPrefab> entities = new List<NamedPrefab>();

        [Tooltip("지형: ground, stone, fence, tree, water, wild_fire, grass, ash")]
        public List<NamedPrefab> terrain = new List<NamedPrefab>();

        [Tooltip("이펙트: splash, steam, ignite, smoke, goat_shockwave, cow_impact, wolf_merge_dust, sheep_eaten, sheep_arrive, eat_crumbs, power_charge, uppercut_impact")]
        public List<NamedPrefab> effects = new List<NamedPrefab>();

        [Tooltip("shift 포인트 화살표. 오른쪽을 가리키는 그림을 돌려서 쓴다.")]
        public GameObject shiftArrow;

        [Tooltip("마우스를 올린 줄의 칸마다 깔리는 표시.")]
        public GameObject lineHighlight;

        public GameObject Entity(string prefabName) => Find(entities, prefabName);
        public GameObject Terrain(string prefabName) => Find(terrain, prefabName);
        public GameObject Effect(string prefabName) => Find(effects, prefabName);

        private static GameObject Find(List<NamedPrefab> list, string prefabName)
        {
            foreach (var entry in list)
                if (entry.name == prefabName) return entry.prefab;
            return null;
        }
    }
}
