using System;
using System.Collections.Generic;
using Munbanggu.Shop;
using Munbanggu.UI;

namespace Munbanggu.Core
{
    /// <summary>
    /// 이야기 컷신 (docs/11): 새 게임 프롤로그, 첫 증축·이사 두 번의 수첩 장, 마지막 동네를 다 지으면 엔딩.
    /// 본 장은 TownSave.story에 남아 다시 안 뜨고, 앨범에서 다시 볼 수 있다.
    /// </summary>
    public partial class GameManager
    {
        bool freshGame;            // 이번 실행에서 새로 시작한 판 (저장 파일 없음)
        CutscenePlayer cutscene;
        readonly Queue<string> storyQueue = new Queue<string>();

        public bool InCutscene => cutscene != null;
        public CutscenePlayer Cutscene => cutscene;
        /// <summary>스크린샷 도구용: 증축·이사 때 이야기가 저절로 뜨지 않게.</summary>
        public static bool AutoStoryOff;
        public bool StorySeen(string id) => Town.story.Contains(id);

        /// <summary>본 장 목록 (앨범 "이야기 다시 보기"용, 이야기 순서대로).</summary>
        public List<StoryChapter> SeenChapters()
        {
            var list = new List<StoryChapter>();
            foreach (var c in StoryTable.All) if (StorySeen(c.id)) list.Add(c);
            return list;
        }

        /// <summary>컷신 재생. record면 본 것으로 저장. 이미 재생 중이면 끝난 뒤 이어서.</summary>
        public void PlayStory(string id, bool record = true, Action after = null)
        {
            var ch = StoryTable.Find(id);
            if (ch == null || Layout == null) { after?.Invoke(); return; }
            if (record && !Town.story.Contains(id)) { Town.story.Add(id); Save(); }
            if (cutscene != null) { storyQueue.Enqueue(id); return; }
            var cam = Layout.Camera;
            cutscene = CutscenePlayer.Play(ch, cam, cam != null ? cam.GetComponent<ShopCamera>() : null, Layout.Root, () =>
            {
                cutscene = null;
                after?.Invoke();
                if (storyQueue.Count > 0) PlayStory(storyQueue.Dequeue(), false);
            });
        }

        /// <summary>게임을 켠 직후: 새 판이면 프롤로그, 아니면 놓친 장이 있는지.</summary>
        void StoryOnStart()
        {
            if (freshGame && !StorySeen(StoryTable.Prologue)) PlayStory(StoryTable.Prologue);
            else CheckStoryMilestones();
        }

        /// <summary>증축·이사 뒤, 그리고 켤 때: 조건을 채웠는데 아직 안 본 장을 순서대로.</summary>
        void CheckStoryMilestones()
        {
            if (AutoStoryOff) return;
            int last = ExpansionTable.Districts.Length - 1;
            if ((District > 0 || Expansion >= 1) && !StorySeen(StoryTable.Expand)) PlayStory(StoryTable.Expand);
            if (District >= 1 && !StorySeen(StoryTable.Move1)) PlayStory(StoryTable.Move1);
            if (District >= 2 && !StorySeen(StoryTable.Move2)) PlayStory(StoryTable.Move2);
            if (District >= last && Expansion >= ExpansionTable.Max && !StorySeen(StoryTable.Ending)) PlayStory(StoryTable.Ending);
        }
    }
}
