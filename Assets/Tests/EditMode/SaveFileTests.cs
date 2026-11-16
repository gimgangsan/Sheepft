using NUnit.Framework;
using Sheepft.Game;

namespace Sheepft.Tests
{
    /// <summary>
    /// 저장 파일의 규칙. 구현 계획 11절. 실제 저장소(PlayerPrefs, localStorage)는 건드리지 않고 SaveFile만 본다.
    /// </summary>
    public class SaveFileTests
    {
        [Test]
        public void NewFile_OnlyFirstStageOpen_DefaultSettings()
        {
            var file = new SaveFile();
            Assert.AreEqual(1, file.unlocked);
            Assert.AreEqual(0, file.Stars(0));
            Assert.AreEqual(SaveFile.DefaultVolume, file.music);
            Assert.AreEqual(SaveFile.DefaultVolume, file.sound);
            Assert.AreEqual(SaveFile.DefaultSpeed, file.speed);
        }

        [Test]
        public void RecordClear_KeepsBestStars_AndOpensNextStage()
        {
            var file = new SaveFile();
            file.RecordClear(0, 2, 30);
            file.RecordClear(0, 1, 30);
            Assert.AreEqual(2, file.Stars(0));
            Assert.AreEqual(2, file.unlocked);

            file.RecordClear(0, 3, 30);
            Assert.AreEqual(3, file.Stars(0));

            // 마지막 스테이지를 깨도 스테이지 수를 넘겨 열지 않는다.
            file.RecordClear(29, 1, 30);
            Assert.AreEqual(30, file.unlocked);
        }

        [Test]
        public void Json_RoundTrips()
        {
            var file = new SaveFile();
            file.RecordClear(4, 3, 30);
            file.lastStage = 4;
            file.speed = 0;
            file.MatchStages("abc");

            var back = SaveFile.FromJson(file.ToJson());
            Assert.AreEqual(3, back.Stars(4));
            Assert.AreEqual(6, back.unlocked);
            Assert.AreEqual(4, back.lastStage);
            Assert.AreEqual(0, back.speed);
            Assert.AreEqual("abc", back.stages);
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("not json")]
        [TestCase("{\"version\":999,\"unlocked\":7}")]
        public void BrokenOrOtherVersion_StartsFresh(string json)
        {
            var file = SaveFile.FromJson(json);
            Assert.AreEqual(SaveFile.CurrentVersion, file.version);
            Assert.AreEqual(1, file.unlocked);
            Assert.IsNotNull(file.stars);
        }

        [Test]
        public void ChangedStages_ResetProgress_KeepSettings()
        {
            var file = new SaveFile();
            Assert.IsFalse(file.MatchStages("first"), "처음 지문을 적을 때는 지울 것이 없다");
            file.RecordClear(0, 3, 30);
            file.lastStage = 1;
            file.music = 5;
            file.speed = 3;

            Assert.IsFalse(file.MatchStages("first"));
            Assert.AreEqual(3, file.Stars(0));

            Assert.IsTrue(file.MatchStages("second"));
            Assert.AreEqual(0, file.Stars(0));
            Assert.AreEqual(1, file.unlocked);
            Assert.AreEqual(0, file.lastStage);
            Assert.AreEqual(5, file.music);
            Assert.AreEqual(3, file.speed);
            Assert.AreEqual("second", file.stages);
        }

        [Test]
        public void Fingerprint_IgnoresLineEndings_ButSeesContent()
        {
            string lf = "# A\n.S .\n";
            string crlf = "# A\r\n.S .\r\n";
            Assert.AreEqual(SaveFile.StagesFingerprint(lf), SaveFile.StagesFingerprint(crlf));
            Assert.AreNotEqual(SaveFile.StagesFingerprint(lf), SaveFile.StagesFingerprint("# A\n.S .S\n"));
        }

        [Test]
        public void Fingerprint_OfRealStagesFile_IsStable()
        {
            string text = StagesFileTests.ReadStagesText();
            Assert.AreEqual(SaveFile.StagesFingerprint(text), SaveFile.StagesFingerprint(text));
            Assert.AreEqual(16, SaveFile.StagesFingerprint(text).Length);
        }
    }
}
