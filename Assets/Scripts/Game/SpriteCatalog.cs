using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>.aseprite 파일의 태그 하나. 프레임 번호는 0부터, to는 포함이다.</summary>
    [Serializable]
    public sealed class SpriteTag
    {
        public string name;
        public int from;
        public int to;
    }

    /// <summary>.aseprite 파일 하나의 프레임, 프레임 길이, 태그.</summary>
    [Serializable]
    public sealed class SpriteSheet
    {
        /// <summary>확장자를 뺀 파일 이름. 예: `sheep`, `wild_fire`</summary>
        public string name;

        /// <summary>프레임 번호 순서. 비어 있는 프레임은 null이다.</summary>
        public Sprite[] frames = new Sprite[0];

        public int[] durationsMs = new int[0];
        public SpriteTag[] tags = new SpriteTag[0];

        public Sprite Frame(int index)
        {
            if (frames.Length == 0) return null;
            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        public SpriteTag Tag(string tagName)
        {
            foreach (var tag in tags)
                if (tag.name == tagName) return tag;
            return null;
        }

        /// <summary>태그의 첫 프레임. 태그가 없으면 0번 프레임.</summary>
        public Sprite TagFrame(string tagName)
        {
            var tag = Tag(tagName);
            return Frame(tag == null ? 0 : tag.from);
        }
    }

    /// <summary>
    /// 게임이 쓰는 모든 스프라이트와 레벨 파일의 목록.
    /// 내용은 손으로 채우지 않고 메뉴 Sheepft > Setup Game Scene이 Assets/Sprites를 읽어 채운다.
    /// </summary>
    public sealed class SpriteCatalog : MonoBehaviour
    {
        public TextAsset stages;

        /// <summary>화면 연출의 조정값. 비어 있으면 기본값을 쓴다.</summary>
        public GameSettings settings;

        private GameSettings fallbackSettings;

        public GameSettings Settings
        {
            get
            {
                if (settings != null) return settings;
                if (fallbackSettings == null) fallbackSettings = ScriptableObject.CreateInstance<GameSettings>();
                return fallbackSettings;
            }
        }
        public List<SpriteSheet> sheets = new List<SpriteSheet>();

        private Dictionary<string, SpriteSheet> byName;

        /// <summary>없으면 null. 스프라이트가 아직 없는 오브젝트는 그리지 않는다.</summary>
        public SpriteSheet Get(string sheetName)
        {
            if (byName == null)
            {
                byName = new Dictionary<string, SpriteSheet>();
                foreach (var sheet in sheets) byName[sheet.name] = sheet;
            }
            SpriteSheet found;
            return byName.TryGetValue(sheetName, out found) ? found : null;
        }
    }
}
