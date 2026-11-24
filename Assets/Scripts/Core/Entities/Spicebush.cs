namespace Sheepft.Core
{
    /// <summary>
    /// 생강꽃. 엔딩 스테이지의 물건. 스스로 하는 일은 없고 shift, 밀치기, 운반, 소의 밀기로 움직인다.
    /// 양과 같은 칸이 되면 그 양이 먹고 근육질 양이 된다(겹침 상호작용). README 6절.
    /// 맨 가지에 핀 꽃다발이라 나무 재질로 본다(탐).
    /// </summary>
    public sealed class Spicebush : ObjectEntity
    {
        public override bool IsWooden => true;

        /// <summary>
        /// 같은 칸의 근육질이 아닌 양 가운데 Id가 가장 작은 양이 먹는다. 생강꽃 하나에 양 한 마리다.
        /// 근육질 양만 있으면 먹지 않고 그대로 둔다.
        /// </summary>
        public override bool OnOverlap(Board b, Tile t)
        {
            Sheep eater = null;
            foreach (var o in t.Objects) // 칸의 목록은 Id 순서다.
            {
                var sheep = o as Sheep;
                if (sheep != null && !sheep.Buff) { eater = sheep; break; }
            }
            if (eater == null) return false;

            b.Remove(this, RemoveCause.Eaten);
            b.SetBuff(eater, true);
            b.Mark(Mechanic.SheepBuff);
            return true;
        }
    }
}
