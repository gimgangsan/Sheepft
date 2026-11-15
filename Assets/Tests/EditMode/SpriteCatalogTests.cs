using NUnit.Framework;
using Sheepft.Game;
using UnityEditor;
using UnityEngine;

namespace Sheepft.Tests
{
    /// <summary>
    /// GameRoot 프리팹의 스프라이트 목록이 화면 코드가 기대하는 모양인지 본다.
    /// 실패하면 메뉴 Sheepft > Setup Game Scene을 다시 돌린다.
    /// </summary>
    public class SpriteCatalogTests
    {
        private static SpriteCatalog Load()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GameRoot.prefab");
            Assert.IsNotNull(prefab, "GameRoot 프리팹이 없다.");
            var catalog = prefab.GetComponent<SpriteCatalog>();
            Assert.IsNotNull(catalog);
            return catalog;
        }

        [Test]
        public void Catalog_HasStagesFileAndGameController()
        {
            var catalog = Load();
            Assert.IsNotNull(catalog.stages, "stages.txt 참조");
            Assert.IsNotNull(catalog.GetComponent<GameController>());
        }

        [Test]
        public void Catalog_EverySheetHasItsFirstFrame()
        {
            var catalog = Load();
            Assert.AreEqual(36, catalog.sheets.Count);
            foreach (var sheet in catalog.sheets)
            {
                Assert.Greater(sheet.frames.Length, 0, sheet.name);
                Assert.AreEqual(sheet.frames.Length, sheet.durationsMs.Length, sheet.name);
                Assert.IsNotNull(sheet.frames[0], sheet.name + "의 0번 프레임");
            }
        }

        [Test]
        public void Catalog_SpritesUse32PixelsPerUnit()
        {
            foreach (var sheet in Load().sheets)
                Assert.AreEqual(32f, sheet.frames[0].pixelsPerUnit, sheet.name);
        }

        // 화면 코드가 이름으로 찾는 파일과 태그.
        [TestCase("sheep", "idle")]
        [TestCase("wolf", "idle")]
        [TestCase("goat", "idle")]
        [TestCase("horse", "idle")]
        [TestCase("cow", "idle")]
        [TestCase("wooden_bucket", "empty")]
        [TestCase("wooden_bucket", "full")]
        [TestCase("wild_fire", "idle")]
        [TestCase("status_icons", "wet")]
        [TestCase("status_icons", "burned")]
        [TestCase("shift_arrow", "normal")]
        [TestCase("shift_arrow", "hover")]
        [TestCase("shift_arrow", "disabled")]
        [TestCase("shift_arrow", "blocked")]
        [TestCase("line_highlight", "idle")]
        [TestCase("button_undo", "normal")]
        [TestCase("button_undo", "hover")]
        [TestCase("button_undo", "pressed")]
        [TestCase("button_restart", "normal")]
        [TestCase("button_next", "normal")]
        [TestCase("hud_icons", "turn")]
        [TestCase("hud_icons", "goal_sheep")]
        [TestCase("cannot_win", "idle")]
        public void Catalog_HasTag(string sheetName, string tagName)
        {
            var sheet = Load().Get(sheetName);
            Assert.IsNotNull(sheet, sheetName);
            var tag = sheet.Tag(tagName);
            Assert.IsNotNull(tag, sheetName + "의 태그 " + tagName);
            for (int i = tag.from; i <= tag.to; i++)
                Assert.IsNotNull(sheet.frames[i], sheetName + "의 " + i + "번 프레임");
        }

        // 프레임 번호로 고르는 파일: 연결 타일(이웃 마스크 0–15)과 숫자(0–9).
        [TestCase("water", 16)]
        [TestCase("grass", 16)]
        [TestCase("fence", 16)]
        [TestCase("digits", 10)]
        [TestCase("ground", 1)]
        [TestCase("stone", 1)]
        [TestCase("tree", 1)]
        [TestCase("ash", 1)]
        [TestCase("fruit_basket", 1)]
        public void Catalog_HasFrameCount(string sheetName, int frames)
        {
            var sheet = Load().Get(sheetName);
            Assert.IsNotNull(sheet, sheetName);
            Assert.AreEqual(frames, sheet.frames.Length);
            foreach (var frame in sheet.frames) Assert.IsNotNull(frame, sheetName);
        }

        [Test]
        public void Catalog_SpritePivotIsBottomCenterOfCanvas()
        {
            // 화면 코드는 모든 그림을 칸의 아래쪽 가운데에 놓는다. 솟아오르는 장애물은 32×48이다.
            // Importer는 투명한 가장자리를 잘라 rect를 만들고 pivot을 그 rect 기준으로 적는다.
            // 그래서 캔버스를 꽉 채우는 프레임으로 확인한다. 연결 타일은 이웃이 사방에 있는 15번이 꽉 찬다
            // (0번처럼 혼자 있는 물·풀은 테두리가 투명해서 잘린다).
            var catalog = Load();
            foreach (var (name, frame) in new[] { ("ground", 0), ("water", 15), ("grass", 15) })
            {
                var sprite = catalog.Get(name).frames[frame];
                Assert.AreEqual(16f, sprite.pivot.x, 0.01f, name);
                Assert.AreEqual(0f, sprite.pivot.y, 0.01f, name);
                Assert.AreEqual(32f, sprite.rect.width, name);
                Assert.AreEqual(32f, sprite.rect.height, name);
            }
        }
    }
}
