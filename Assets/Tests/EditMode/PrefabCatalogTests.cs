using NUnit.Framework;
using Sheepft.Core;
using Sheepft.Game;
using UnityEditor;
using UnityEngine;

namespace Sheepft.Tests
{
    /// <summary>
    /// GameRoot의 프리팹 목록과 UI 프리팹이 화면 코드가 기대하는 모양인지 본다.
    /// 실패하면 메뉴 Sheepft > Setup Game Scene을 돌린다(비어 있는 것만 채운다).
    /// </summary>
    public class PrefabCatalogTests
    {
        private static PrefabCatalog Load()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameRoot.prefab");
            Assert.IsNotNull(root, "GameRoot 프리팹이 없다.");
            var catalog = root.GetComponent<PrefabCatalog>();
            Assert.IsNotNull(catalog, "GameRoot에 PrefabCatalog가 없다.");
            return catalog;
        }

        [Test]
        public void Catalog_HasEveryEntityPrefab_WithBodyAndAnimators()
        {
            var catalog = Load();
            foreach (var name in new[] { "sheep", "wolf", "goat", "horse", "cow", "fruit_basket", "wooden_bucket" })
            {
                var prefab = catalog.Entity(name);
                Assert.IsNotNull(prefab, name + " 프리팹");
                var view = prefab.GetComponent<EntityView>();
                Assert.IsNotNull(view, name + "의 EntityView");
                Assert.IsNotNull(view.body, name + "의 body");
                Assert.IsNotNull(view.body.sprite, name + "의 body 그림");
                Assert.IsNotNull(view.bodyAnimator, name + "의 bodyAnimator");
                // 과일 바구니는 한 프레임짜리라 Aseprite Importer가 Animator Controller를 만들지 않는다.
                if (name != "fruit_basket")
                    Assert.IsNotNull(view.bodyAnimator.runtimeAnimatorController, name + "의 Animator Controller");
                Assert.IsNotNull(view.icon, name + "의 icon");
                Assert.IsNotNull(view.iconAnimator, name + "의 iconAnimator");
                Assert.IsNotNull(view.iconAnimator.runtimeAnimatorController, name + "의 아이콘 Animator Controller");
            }
        }

        [Test]
        public void Catalog_HasEveryTerrainPrefab()
        {
            var catalog = Load();
            foreach (TerrainType type in System.Enum.GetValues(typeof(TerrainType)))
            {
                string name = BoardView.SheetNameOf(type);
                var prefab = catalog.Terrain(name);
                Assert.IsNotNull(prefab, name + " 프리팹");
                Assert.IsNotNull(prefab.GetComponentInChildren<SpriteRenderer>(), name + "의 SpriteRenderer");
            }
        }

        [Test]
        public void Catalog_HasEveryEffectPrefab_ArrowAndHighlight()
        {
            var catalog = Load();
            foreach (var name in new[]
            {
                "splash", "steam", "ignite", "smoke", "goat_shockwave", "cow_impact",
                "wolf_merge_dust", "sheep_eaten", "sheep_arrive", "eat_crumbs",
            })
            {
                var prefab = catalog.Effect(name);
                Assert.IsNotNull(prefab, name + " 프리팹");
                var effect = prefab.GetComponent<OneShotEffect>();
                Assert.IsNotNull(effect, name + "의 OneShotEffect");
                Assert.IsNotNull(effect.animator, name + "의 Animator");
                Assert.IsNotNull(effect.animator.runtimeAnimatorController, name + "의 Animator Controller");
            }
            Assert.IsNotNull(catalog.shiftArrow, "shift 화살표 프리팹");
            Assert.IsNotNull(catalog.shiftArrow.GetComponentInChildren<SpriteRenderer>());
            Assert.IsNotNull(catalog.lineHighlight, "줄 표시 프리팹");
        }

        [Test]
        public void GameUiPrefab_HasEveryReference()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/GameUI.prefab");
            Assert.IsNotNull(prefab, "GameUI 프리팹이 없다.");
            var ui = prefab.GetComponent<GameUI>();
            Assert.IsNotNull(ui);
            foreach (var field in typeof(GameUI).GetFields())
            {
                if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;
                var value = field.GetValue(ui) as Object;
                Assert.IsTrue(value != null, "GameUI." + field.Name + "가 비어 있다.");
            }
            Assert.IsNotNull(ui.turnsText.font, "숫자 폰트");
            StringAssert.Contains("AgravePro", ui.turnsText.font.name);
        }
    }
}
