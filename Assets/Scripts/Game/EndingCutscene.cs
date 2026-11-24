using System;
using System.Collections;
using UnityEngine;

namespace Sheepft.Game
{
    /// <summary>
    /// 엔딩 컷씬. 근육질 양이 늑대를 쓰러뜨리면(마지막 스테이지) GameController가 판 재생을 마친 뒤 부른다.
    ///
    /// 1. 양과 늑대만 남기고 화면과 UI가 검게 가려진다. 늑대는 양 앞으로 물러선다.
    /// 2. 양이 힘을 모은다(5·6프레임 뒤 7·8프레임 반복). 화면이 미세하게 떨리며 양과 늑대 쪽으로 다가간다.
    /// 3. 그동안 왼쪽 주먹에 power_charge가 반복된다.
    /// 4. 10프레임에서 떨림이 멈추고 시간이 멈춘다. uppercut_impact가 3프레임까지 나오고 멈춘다.
    /// 5. 시간이 다시 흐르면 늑대가 화면 밖으로 날아간다.
    /// 6. 동시에 양과 이펙트가 끝까지 재생되고, 화면이 빠르게 물러나며 크게 떨린다. 이어서 천천히 하얗게 덮인다.
    /// 7. 원래 게임 화면으로 돌아온다. 결과 창(별 3개)은 beforeReveal에서 GameController가 띄운다.
    ///
    /// 양·늑대·이펙트는 프리팹을 새로 찍은 대역이고, 프레임은 SpriteCatalog로 직접 넘긴다(Animator를 끈다).
    /// 판의 양은 그동안 숨긴다. 시간은 이동 속도 설정과 상관없고, 모든 시간(프레임 길이 포함)에
    /// endingTimeScale을 곱한다(컷씬 시계를 그만큼 느리게 돌린다). 조정값은 GameSettings의 "엔딩 컷씬".
    /// </summary>
    public sealed class EndingCutscene : MonoBehaviour
    {
        // 정렬 순서. 판(행마다 20씩, 화살표 1000)보다 위에 그린다.
        private const int DarkOrder = 3000;
        private const int SheepOrder = 3010;
        private const int WolfOrder = 3012;
        private const int ChargeOrder = 3020;
        private const int ImpactOrder = 3022;
        private const int FadeOrder = 3100;
        private const float Pixel = 1f / 32f;

        /// <summary>buff_sheep.aseprite의 캔버스 크기(픽셀). 주먹 위치를 캔버스 좌표로 적기 때문에 쓴다.</summary>
        private const float BuffCanvas = 80f;

        // buff_sheep의 프레임 번호(0부터). SPRITES.md 7절은 1부터 세므로 1씩 작다.
        private const int IdleFrom = 0, IdleTo = 3;
        private const int Crouch = 4, WindUp = 5, ChargeA = 6, ChargeB = 7, Swing = 8, Hit = 9;
        private static readonly int[] AfterHit = { 10, 11, 12 };

        // uppercut_impact의 프레임 번호(0부터). 시간이 멈춘 동안은 0–2를 재생하고 2에서 멈춘다.
        private const int ImpactHoldFrame = 2;

        private SpriteCatalog catalog;
        private PrefabCatalog prefabs;
        private Camera cam;
        private GameUI ui;
        private Action restoreCamera;

        private GameObject root;
        private SpriteRenderer dark, fade;
        private SpriteRenderer sheepBody, wolfBody, impactBody;
        private Transform sheepActor, wolfActor, chargeEffect, impactEffect;
        private EntityView hiddenView;
        private CanvasGroup uiGroup;
        private float startedAt;

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// 클릭이나 키로 건너뛸 수 있는가. 시작하고 조금 지나야 건너뛸 수 있다.
        /// 판 재생을 빨리 감던 클릭이 컷씬까지 건너뛰지 않게 하려는 것이다.
        /// </summary>
        public bool CanSkip => !IsPlaying || Time.time - startedAt >= Settings.endingSkipDelaySeconds;

        private GameSettings Settings => catalog.Settings;

        /// <summary>컷씬 전체 시간의 배율(GameSettings.endingTimeScale). 1보다 크면 느려진다.</summary>
        private float TimeScale => Mathf.Max(0.1f, Settings.endingTimeScale);

        /// <summary>이번 프레임에 컷씬 시계가 가는 시간. 모든 시간과 프레임 길이를 이 시계로 잰다.</summary>
        private float Dt => Time.deltaTime / TimeScale;

        public void Init(SpriteCatalog spriteCatalog, PrefabCatalog prefabCatalog, Camera camera, GameUI gameUi, Action fitCamera)
        {
            catalog = spriteCatalog;
            prefabs = prefabCatalog;
            cam = camera;
            ui = gameUi;
            restoreCamera = fitCamera;
        }

        /// <summary>
        /// 컷씬을 끝까지 재생한다. side는 늑대가 양의 어느 쪽에 서는가(1 오른쪽, -1 왼쪽)이고 양은 그쪽을 본다.
        /// beforeReveal은 하얗게 덮인 동안 원래 화면으로 되돌린 뒤 부른다. 결과 창을 여기서 띄우면 막이 걷히며 보인다.
        /// 그림이 없어 재생할 수 없으면 beforeReveal만 부르고 끝난다.
        /// </summary>
        public IEnumerator Play(EntityView sheepView, int side, Action beforeReveal)
        {
            Abort();
            var sheepSheet = catalog.Get("buff_sheep");
            if (sheepView == null || cam == null || sheepSheet == null || sheepSheet.frames.Length <= AfterHit[AfterHit.Length - 1])
            {
                if (beforeReveal != null) beforeReveal();
                yield break;
            }

            IsPlaying = true;
            startedAt = Time.time;
            var s = Settings;
            side = side < 0 ? -1 : 1;
            var wolfSheet = catalog.Get("wolf");
            var impactSheet = catalog.Get("uppercut_impact");

            // ── 준비 ──
            root = new GameObject("ending");
            root.transform.SetParent(transform, false);
            float size0 = cam.orthographicSize;
            Vector3 camera0 = cam.transform.position;

            dark = Cover("dark", DarkOrder, Color.black, camera0);
            fade = Cover("fade", FadeOrder, s.endingFadeColor, camera0);

            Vector3 sheepBase = sheepView.transform.position;
            sheepBase.z = 0f;
            hiddenView = sheepView;
            sheepView.SetFlip(side < 0); // 끝난 뒤에도 늑대가 있던 쪽을 보고 서 있게.
            sheepView.gameObject.SetActive(false);

            sheepActor = Actor("buff_sheep", out sheepBody, SheepOrder);
            sheepActor.position = sheepBase;
            if (sheepBody != null) sheepBody.flipX = side < 0;

            // 늑대는 몸 가운데를 축으로 돈다. 뿌리를 몸 가운데에 두고 프리팹(발밑이 피벗)을 반 칸 내린다.
            var wolfPivot = new GameObject("wolf").transform;
            wolfPivot.SetParent(root.transform, false);
            var wolfPrefab = Actor("wolf", out wolfBody, WolfOrder);
            if (wolfPrefab != null)
            {
                wolfPrefab.SetParent(wolfPivot, false);
                wolfPrefab.localPosition = new Vector3(0f, -0.5f, 0f);
            }
            if (wolfBody != null) wolfBody.flipX = side > 0; // 늑대 그림은 오른쪽을 본다. 양을 보게 돌린다.
            wolfActor = wolfPivot;
            Vector3 wolfStart = sheepBase + Vector3.up * 0.5f;
            Vector3 wolfFront = sheepBase + new Vector3(side * s.endingWolfFrontPixels * Pixel, 0.5f, 0f);
            wolfActor.position = wolfStart;

            uiGroup = UiGroup();
            if (uiGroup != null)
            {
                uiGroup.interactable = false;
                uiGroup.blocksRaycasts = false;
            }

            float wolfClock = 0f;

            // ── 1. 어두워짐 ──
            for (float t = 0f; t < s.endingDarkenSeconds; t += Dt)
            {
                float k = t / s.endingDarkenSeconds;
                SetAlpha(dark, k);
                if (uiGroup != null) uiGroup.alpha = 1f - k;
                wolfActor.position = Vector3.Lerp(wolfStart, wolfFront, 1f - (1f - k) * (1f - k));
                ShowLoop(sheepBody, sheepSheet, IdleFrom, IdleTo, t);
                ShowLoop(wolfBody, wolfSheet, "idle", wolfClock += Dt);
                yield return null;
            }
            SetAlpha(dark, 1f);
            if (uiGroup != null) uiGroup.alpha = 0f;
            wolfActor.position = wolfFront;

            // ── 2·3. 힘 모으기: 5·6프레임 한 번, 7·8프레임 반복. 줌 인, 미세한 떨림, 주먹에 power_charge ──
            Vector3 focus = (sheepBase + new Vector3(wolfFront.x, sheepBase.y, 0f)) * 0.5f
                + new Vector3(0f, s.endingFocusHeightPixels * Pixel, 0f);
            focus.z = camera0.z;
            float size1 = size0 / Mathf.Max(1f, s.endingFocusZoom);
            float crouch = Seconds(sheepSheet, Crouch), windUp = Seconds(sheepSheet, WindUp);
            float chargeA = Seconds(sheepSheet, ChargeA), chargeB = Seconds(sheepSheet, ChargeB);
            float charging = crouch + windUp + s.endingChargeSeconds;
            for (float t = 0f; t < charging; t += Dt)
            {
                int frame;
                if (t < crouch) frame = Crouch;
                else if (t < crouch + windUp) frame = WindUp;
                else frame = Mathf.Repeat(t - crouch - windUp, chargeA + chargeB) < chargeA ? ChargeA : ChargeB;
                Show(sheepBody, sheepSheet, frame);

                // power_charge는 몸을 감아 주먹을 뒤로 뺀 자세(6–8프레임)부터 나온다.
                if (frame >= WindUp && chargeEffect == null)
                {
                    chargeEffect = Effect("power_charge", FistPoint(sheepBase, s.endingChargeFist, side), ChargeOrder, out _);
                    SoundManager.Play(Sound.UppercutCharge);
                }

                float k = Mathf.SmoothStep(0f, 1f, t / charging);
                PlaceCamera(Vector3.Lerp(camera0, focus, k), Mathf.Lerp(size0, size1, k), s.endingChargeShakePixels);
                ShowLoop(wolfBody, wolfSheet, "idle", wolfClock += Dt);
                yield return null;
            }

            // 9프레임: 몸을 풀며 주먹이 배 앞을 가로지른다. 기운은 사라지고 떨림은 이어진다.
            DestroyChild(ref chargeEffect);
            float swing = Seconds(sheepSheet, Swing);
            for (float t = 0f; t < swing; t += Dt)
            {
                Show(sheepBody, sheepSheet, Swing);
                PlaceCamera(focus, size1, s.endingChargeShakePixels);
                yield return null;
            }

            // ── 4. 10프레임: 떨림이 멈추고 시간이 멈춘다. 늑대는 주먹에 맞아 떠오른 자세로 멈춘다 ──
            Show(sheepBody, sheepSheet, Hit);
            PlaceCamera(focus, size1, 0f);
            Vector3 fist = FistPoint(sheepBase, s.endingHitFist, side);
            wolfActor.position = fist + new Vector3(side * s.endingWolfHitOffsetPixels.x, s.endingWolfHitOffsetPixels.y, 0f) * Pixel;
            wolfActor.rotation = Quaternion.Euler(0f, 0f, -side * s.endingWolfHitTilt);
            impactEffect = Effect("uppercut_impact", fist, ImpactOrder, out impactBody);
            if (impactBody != null) impactBody.flipX = side < 0;
            // 모으던 소리를 끊고 타격음.
            SoundManager.Stop(Sound.UppercutCharge);
            SoundManager.Play(Sound.WolfHit);

            float impactToHold = 0f;
            for (int i = 0; i <= ImpactHoldFrame; i++) impactToHold += Seconds(impactSheet, i);
            float stop = Mathf.Max(s.endingHitstopSeconds, impactToHold);
            for (float t = 0f; t < stop; t += Dt)
            {
                Show(impactBody, impactSheet, FrameAt(impactSheet, 0, ImpactHoldFrame, t, false));
                yield return null;
            }

            // ── 5·6. 시간이 다시 흐른다: 늑대가 날아가고, 양과 이펙트가 끝까지 재생되고, 화면이 물러나며 크게 떨린다 ──
            Vector3 fly = new Vector3(side * s.endingWolfFlyDirection.x, s.endingWolfFlyDirection.y, 0f);
            fly = fly.sqrMagnitude > 0f ? fly.normalized * s.endingWolfFlySpeed : Vector3.zero;
            float spin = -side * s.endingWolfSpinPerSecond;
            float sheepRest = 0f;
            foreach (var f in AfterHit) sheepRest += Seconds(sheepSheet, f);
            float impactRest = 0f;
            int impactLast = impactSheet != null ? impactSheet.frames.Length - 1 : ImpactHoldFrame;
            for (int i = ImpactHoldFrame + 1; i <= impactLast; i++) impactRest += Seconds(impactSheet, i);
            float after = Mathf.Max(Mathf.Max(s.endingWolfFlySeconds, s.endingZoomOutSeconds),
                Mathf.Max(s.endingBigShakeSeconds, Mathf.Max(sheepRest, impactRest)));
            SoundManager.Play(Sound.WolfYelp);

            for (float t = 0f; t < after; t += Dt)
            {
                float dt = Dt;
                if (wolfActor != null)
                {
                    if (t < s.endingWolfFlySeconds)
                    {
                        wolfActor.position += fly * dt;
                        wolfActor.Rotate(0f, 0f, spin * dt);
                    }
                    else
                    {
                        wolfActor.gameObject.SetActive(false);
                    }
                }

                if (t < sheepRest) Show(sheepBody, sheepSheet, Sequence(sheepSheet, AfterHit, t));
                else ShowLoop(sheepBody, sheepSheet, IdleFrom, IdleTo, t - sheepRest);

                if (impactEffect != null)
                {
                    if (t < impactRest) Show(impactBody, impactSheet, FrameAt(impactSheet, ImpactHoldFrame + 1, impactLast, t, false));
                    else DestroyChild(ref impactEffect);
                }

                float zoom = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / s.endingZoomOutSeconds), 3f);
                float shake = s.endingBigShakeSeconds > 0f ? 1f - Mathf.Clamp01(t / s.endingBigShakeSeconds) : 0f;
                PlaceCamera(Vector3.Lerp(focus, camera0, zoom), Mathf.Lerp(size1, size0, zoom), s.endingBigShakePixels * shake * shake);
                yield return null;
            }
            DestroyChild(ref impactEffect);
            if (wolfActor != null) wolfActor.gameObject.SetActive(false);
            PlaceCamera(camera0, size0, 0f);

            // ── 6. 천천히 하얗게 덮인다 ──
            float idleClock = 0f;
            for (float t = 0f; t < s.endingWhiteSeconds; t += Dt)
            {
                SetAlpha(fade, t / s.endingWhiteSeconds);
                ShowLoop(sheepBody, sheepSheet, IdleFrom, IdleTo, idleClock += Dt);
                yield return null;
            }
            SetAlpha(fade, 1f);
            if (s.endingWhiteHoldSeconds > 0f) yield return new WaitForSeconds(s.endingWhiteHoldSeconds * TimeScale);

            // ── 7. 막 뒤에서 원래 화면으로 되돌리고, 막을 걷는다 ──
            RestoreBoard();
            if (beforeReveal != null) beforeReveal();
            for (float t = 0f; t < s.endingRevealSeconds; t += Dt)
            {
                float k = t / s.endingRevealSeconds;
                SetAlpha(fade, 1f - k);
                if (uiGroup != null) uiGroup.alpha = k;
                yield return null;
            }
            Abort();
        }

        /// <summary>
        /// 컷씬을 바로 끝내고 모든 것을 원래대로 돌린다(대역과 막을 지우고, 카메라·UI·판의 양을 되살린다).
        /// 건너뛰기, undo, 스테이지 바꾸기에서 부른다. 재생 중이 아니면 아무것도 하지 않는다.
        /// </summary>
        public void Abort()
        {
            if (!IsPlaying && root == null) return;
            // 건너뛰면 컷씬 소리가 게임 화면까지 이어지지 않게 끊는다.
            SoundManager.Stop(Sound.UppercutCharge);
            SoundManager.Stop(Sound.WolfYelp);
            RestoreBoard();
            if (root != null) Destroy(root);
            root = null;
            dark = fade = null;
            if (uiGroup != null)
            {
                uiGroup.alpha = 1f;
                uiGroup.interactable = true;
                uiGroup.blocksRaycasts = true;
            }
            IsPlaying = false;
        }

        /// <summary>대역과 검은 막을 지우고 판의 양과 카메라를 되살린다. 흰 막은 남긴다.</summary>
        private void RestoreBoard()
        {
            DestroyChild(ref sheepActor);
            DestroyChild(ref wolfActor);
            DestroyChild(ref chargeEffect);
            DestroyChild(ref impactEffect);
            sheepBody = wolfBody = impactBody = null;
            if (dark != null) SetAlpha(dark, 0f);
            if (hiddenView != null) hiddenView.gameObject.SetActive(true);
            hiddenView = null;
            if (restoreCamera != null) restoreCamera();
        }

        // ── 만들기 ──

        /// <summary>화면 전체를 덮는 단색 막. 처음에는 투명하다.</summary>
        private SpriteRenderer Cover(string coverName, int order, Color color, Vector3 center)
        {
            var renderer = BoardView.NewRenderer(root.transform, coverName, order);
            renderer.sprite = BoardView.WhiteSquare();
            color.a = 0f;
            renderer.color = color;
            renderer.transform.position = new Vector3(center.x, center.y, 0f);
            renderer.transform.localScale = new Vector3(500f, 500f, 1f); // 줌 아웃과 떨림에도 넉넉히 덮는다.
            return renderer;
        }

        /// <summary>
        /// 오브젝트 프리팹을 대역으로 찍는다. EntityView와 Animator를 끄고 몸 그림만 직접 넘긴다. 상태 아이콘은 숨긴다.
        /// </summary>
        private Transform Actor(string sheet, out SpriteRenderer body, int order)
        {
            body = null;
            var prefab = prefabs.Entity(sheet);
            if (prefab == null) return null;
            var go = Instantiate(prefab, root.transform);
            go.name = "actor " + sheet;
            go.transform.localScale = Vector3.one;
            var view = go.GetComponent<EntityView>();
            if (view != null)
            {
                view.enabled = false;
                body = view.body;
                if (view.icon != null) view.icon.enabled = false;
            }
            foreach (var animator in go.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            if (body == null) body = go.GetComponentInChildren<SpriteRenderer>();
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sortingOrder = order;
            return go.transform;
        }

        /// <summary>
        /// 이펙트 프리팹을 그림 중심이 at에 오게 놓는다. 한 번 재생하는 이펙트는 Animator와 OneShotEffect를 끄고
        /// 프레임을 직접 넘긴다(body로 돌려준다). 반복 이펙트(power_charge)는 Animator가 그대로 돈다.
        /// </summary>
        private Transform Effect(string effectName, Vector3 at, int order, out SpriteRenderer body)
        {
            body = null;
            var prefab = prefabs.Effect(effectName);
            if (prefab == null) return null;
            var go = Instantiate(prefab, root.transform);
            go.name = "effect " + effectName;
            go.transform.position = at;
            var oneShot = go.GetComponent<OneShotEffect>();
            if (oneShot != null)
            {
                oneShot.enabled = false;
                foreach (var animator in go.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                body = go.GetComponentInChildren<SpriteRenderer>();
            }
            foreach (var renderer in go.GetComponentsInChildren<SpriteRenderer>(true)) renderer.sortingOrder = order;
            return go.transform;
        }

        private CanvasGroup UiGroup()
        {
            if (ui == null) return null;
            var canvas = ui.GetComponentInParent<Canvas>();
            var host = canvas != null ? canvas.gameObject : ui.gameObject;
            var group = host.GetComponent<CanvasGroup>();
            return group != null ? group : host.AddComponent<CanvasGroup>();
        }

        // ── 위치 ──

        /// <summary>buff_sheep 캔버스의 픽셀(왼쪽 위 기준)이 월드의 어디인가. 양이 왼쪽을 보면 좌우를 뒤집는다.</summary>
        private static Vector3 FistPoint(Vector3 sheepBase, Vector2 canvasPixel, int side)
        {
            float x = (canvasPixel.x + 0.5f - BuffCanvas * 0.5f) * side;
            float y = BuffCanvas - (canvasPixel.y + 0.5f);
            return sheepBase + new Vector3(x, y, 0f) * Pixel;
        }

        /// <summary>카메라를 놓는다. shakePixels만큼 아무 방향으로나 흔든다.</summary>
        private void PlaceCamera(Vector3 position, float size, float shakePixels)
        {
            if (cam == null) return;
            Vector3 shake = shakePixels > 0f ? (Vector3)(UnityEngine.Random.insideUnitCircle * shakePixels * Pixel) : Vector3.zero;
            cam.transform.position = position + shake;
            cam.orthographicSize = size;
        }

        // ── 프레임 ──

        private static float Seconds(SpriteSheet sheet, int frame)
        {
            if (sheet == null || frame < 0 || frame >= sheet.durationsMs.Length) return 0.1f;
            return Mathf.Max(0.01f, sheet.durationsMs[frame] / 1000f);
        }

        private static void Show(SpriteRenderer renderer, SpriteSheet sheet, int frame)
        {
            if (renderer == null || sheet == null) return;
            var sprite = sheet.Frame(frame);
            if (sprite != null) renderer.sprite = sprite;
        }

        /// <summary>from–to 프레임을 각자의 길이대로 t초째에 보일 프레임. loop가 아니면 끝 프레임에서 멈춘다.</summary>
        private static int FrameAt(SpriteSheet sheet, int from, int to, float t, bool loop)
        {
            float total = 0f;
            for (int i = from; i <= to; i++) total += Seconds(sheet, i);
            if (loop && total > 0f) t = Mathf.Repeat(t, total);
            for (int i = from; i <= to; i++)
            {
                t -= Seconds(sheet, i);
                if (t < 0f) return i;
            }
            return to;
        }

        /// <summary>정해 둔 프레임들을 차례로 t초째에 보일 프레임. 끝나면 마지막 프레임.</summary>
        private static int Sequence(SpriteSheet sheet, int[] frames, float t)
        {
            foreach (var f in frames)
            {
                t -= Seconds(sheet, f);
                if (t < 0f) return f;
            }
            return frames[frames.Length - 1];
        }

        private static void ShowLoop(SpriteRenderer renderer, SpriteSheet sheet, int from, int to, float t)
        {
            if (sheet == null) return;
            Show(renderer, sheet, FrameAt(sheet, from, to, t, true));
        }

        private static void ShowLoop(SpriteRenderer renderer, SpriteSheet sheet, string tag, float t)
        {
            var range = sheet != null ? sheet.Tag(tag) : null;
            if (range == null) return;
            ShowLoop(renderer, sheet, range.from, range.to, t);
        }

        private static void SetAlpha(SpriteRenderer renderer, float alpha)
        {
            if (renderer == null) return;
            var color = renderer.color;
            color.a = Mathf.Clamp01(alpha);
            renderer.color = color;
        }

        private static void DestroyChild(ref Transform child)
        {
            if (child != null) Destroy(child.gameObject);
            child = null;
        }
    }
}
