using System;
using System.Collections.Generic;
using Sheepft.Core;

namespace Sheepft.Tools
{
    /// <summary>
    /// 스테이지 에디터가 고치는 stages.txt 전체. 파일 머리 주석과 스테이지 목록.
    /// Unity가 직렬화할 수 있어서 JsonUtility로 통째로 저장해 되돌리기 기록과 도메인 재로드에 쓴다.
    /// </summary>
    [Serializable]
    public sealed class StageFile
    {
        public List<string> comments = new List<string>();
        public List<StageDraft> stages = new List<StageDraft>();

        /// <summary>
        /// 파일 내용을 읽는다. 게임 파서로 읽으므로 표기가 틀렸으면 LevelParseException이 난다.
        /// droppedComments에는 다시 쓸 때 사라지는 주석의 줄 번호가 들어간다.
        /// </summary>
        public static StageFile Load(string text, out List<int> droppedComments)
        {
            var parsed = LevelParser.ParseFile(text);
            var file = new StageFile();
            file.comments.AddRange(parsed.Comments);
            foreach (var board in parsed.Levels) file.stages.Add(StageDraft.FromBoard(board));
            droppedComments = parsed.DroppedCommentLines;
            return file;
        }

        /// <summary>
        /// 저장할 파일 내용. 모든 스테이지를 게임 파서로 읽은 뒤 LevelWriter로 정돈해 쓴다.
        /// 하나라도 읽지 못하면 null이고 errors에 "번호. 이름: 이유"가 들어간다.
        /// </summary>
        public string ToFileText(List<string> errors)
        {
            var boards = new List<Board>();
            var names = new HashSet<string>();
            for (int i = 0; i < stages.Count; i++)
            {
                Board board;
                string error;
                if (!stages[i].TryParse(out board, out error))
                {
                    errors.Add((i + 1) + ". " + stages[i].name + ": " + error);
                    continue;
                }
                if (!names.Add(board.Rules.Name)) errors.Add((i + 1) + ". " + stages[i].name + ": 이름이 다른 스테이지와 겹친다.");
                boards.Add(board);
            }
            if (errors.Count > 0) return null;
            return LevelWriter.WriteFile(comments, boards);
        }

        // ── 목록 ──

        /// <summary>after 바로 뒤에 새 스테이지를 넣고 그 번호를 돌려준다. after가 -1이면 맨 앞.</summary>
        public int AddNew(int after)
        {
            int at = Math.Min(after + 1, stages.Count);
            stages.Insert(at, StageDraft.CreateNew(UniqueName("New Stage")));
            return at;
        }

        /// <summary>index 스테이지를 복제해 바로 뒤에 넣고 그 번호를 돌려준다. 이름 뒤에 " Copy"를 붙인다.</summary>
        public int Duplicate(int index)
        {
            var copy = stages[index].Clone();
            copy.name = UniqueName(stages[index].name + " Copy");
            stages.Insert(index + 1, copy);
            return index + 1;
        }

        public void Delete(int index)
        {
            stages.RemoveAt(index);
        }

        /// <summary>from 스테이지를 to 자리로 옮긴다(옮긴 뒤 그 스테이지의 번호가 to). 범위를 벗어나면 끝에 맞춘다.</summary>
        public int Move(int from, int to)
        {
            to = Math.Max(0, Math.Min(stages.Count - 1, to));
            if (from == to) return to;
            var s = stages[from];
            stages.RemoveAt(from);
            stages.Insert(to, s);
            return to;
        }

        private string UniqueName(string baseName)
        {
            var names = new HashSet<string>();
            foreach (var s in stages) names.Add(s.name);
            if (!names.Contains(baseName)) return baseName;
            for (int n = 2; ; n++)
                if (!names.Contains(baseName + " " + n)) return baseName + " " + n;
        }
    }
}
