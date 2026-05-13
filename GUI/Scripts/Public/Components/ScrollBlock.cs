// Copyright (c) Supernova Technologies LLC
using Nova.Events;
using Nova.Internal;
using Nova.Internal.Input.Scrolling;
using Nova.Internal.Layouts;
using Nova.Internal.Utilities;
using Nova.Internal.Utilities.Extensions;
using Unity.Mathematics;
using UnityEngine;

namespace Nova
{
    /// <summary>
    /// Two-axis scroll: the only <see cref="Layout"/> <b>written</b> at runtime is <see cref="content"/> — <see cref="UIBlock.Layout"/>.<see cref="Layout.Position"/> on X and/or Y.
    /// Scroll extent vs the viewport uses laid-out footprint (<see cref="UIBlock.LayoutSize"/>, then <see cref="UIBlock.CalculatedSize"/>) so the range stays invariant under elastic overscroll; <see cref="UIBlock.ContentSize"/> is only a last resort (child aggregate can grow with bounce displacement). Compared against <see cref="UIBlock.PaddedSize"/>.
    /// The viewport is <see cref="content"/>’s parent <see cref="UIBlock"/> (often Mask) unless <see cref="scrollViewportOverride"/> is set.
    /// </summary>
    [AddComponentMenu("Nova/Scroll Block")]
    [HelpURL("https://novaui.io/manual/Scroller.html")]
    public sealed class ScrollBlock : GestureRecognizer, IInteractable
    {
        #region Public

        [SerializeField]
        public OverscrollEffect OverscrollEffect = OverscrollEffect.Bounce;

        [SerializeField]
        public float VectorScrollMultiplier = 1f;

        [Tooltip("Content root (e.g. Canvas): child of the viewport. Layout.Position scrolls; extent vs the mask prefers LayoutSize / CalculatedSize over ContentSize so bounce does not inflate scroll range.")]
        [SerializeField]
        private UIBlock content;

        [Tooltip("Thumb block for horizontal scrolling. Axis is enabled when this reference is set and the object is active.")]
        [SerializeField]
        private UIBlock horizontalScrollbar;

        [Tooltip("Thumb block for vertical scrolling. Axis is enabled when this reference is set and the object is active.")]
        [SerializeField]
        private UIBlock verticalScrollbar;

        [Tooltip("Optional. When set, this block’s padded size is the viewport for bounds and scrollbar thumb math instead of the content object’s parent (often named Mask in the scene). Read-only; not resized by Scroll Block.")]
        [SerializeField]
        private UIBlock scrollViewportOverride;

        [Tooltip("Reserved for Scroll Block–only bounce tuning. Nova’s ScrollBehavior does not expose per-axis elastic padding; bounce overscroll uses Nova’s fixed elastic band (see Nova ScrollBehavior). Values are not applied at runtime.")]
        [SerializeField]
        private Length maxElasticOverscrollBeyondBoundsX = new Length { Raw = 0.2f, Type = LengthType.Percent };

        [Tooltip("Reserved for Scroll Block–only bounce tuning. Nova’s ScrollBehavior does not expose per-axis elastic padding; bounce overscroll uses Nova’s fixed elastic band (see Nova ScrollBehavior). Values are not applied at runtime.")]
        [SerializeField]
        private Length maxElasticOverscrollBeyondBoundsY = new Length { Raw = 0.2f, Type = LengthType.Percent };

        [Tooltip("Unused; kept for serialized assets. Scroll Block follows Nova ScrollBehavior for bounce/clamp only — no separate recovery pass.")]
        [SerializeField]
        private float overscrollStrictRecoverSpeed = 14f;

        [Tooltip("Unused; kept for serialized assets.")]
        [SerializeField]
        private float overscrollStrictRecoverMaxSeconds = 0.45f;

        [Tooltip("Narrows the strict scroll interval (Nova basis min/max) inward in layout-offset units. Same family as ListView/Scroller using ContentSize vs padded viewport — a small buffer absorbs float drift at the end stops.")]
        [SerializeField]
        private float strictScrollClampBuffer = 0f;

        [Tooltip("Added to strictScrollClampBuffer as a fraction of viewport PaddedSize on that axis (0.01 ≈ 1%).")]
        [SerializeField]
        private float strictScrollClampBufferViewportFraction = 0f;

        [SerializeField]
        private bool dragScrolling = true;

        [SerializeField]
        private bool vectorScrolling = true;

        [Tooltip("When true, pointer drag on the horizontal thumb runs Nova drag and scrolls content. The thumb’s Interactable is set to Draggable on X for that axis (same idea as Scroller + Draggable Scrollbar).")]
        [SerializeField]
        private bool draggableHorizontalScrollbar = true;

        [Tooltip("When true, pointer drag on the vertical thumb runs Nova drag and scrolls content. The thumb’s Interactable is set to Draggable on Y for that axis.")]
        [SerializeField]
        private bool draggableVerticalScrollbar = true;

        public UIBlock Content => content;

        public UIBlock HorizontalScrollbarVisual => horizontalScrollbar;

        public UIBlock VerticalScrollbarVisual => verticalScrollbar;

        /// <summary>
        /// Viewport block used only to <b>read</b> padded size vs <see cref="content"/> (default: content’s parent, often Mask). Never written by <see cref="ScrollBlock"/>.
        /// </summary>
        public UIBlock ScrollViewportBlock => ScrollViewport;

        public bool ScrollHorizontal => AxisEnabled(horizontalScrollbar);

        public bool ScrollVertical => AxisEnabled(verticalScrollbar);

        public bool DragScrolling
        {
            get => dragScrolling;
            set
            {
                if (value == dragScrolling)
                {
                    return;
                }

                dragScrolling = value;
                if (!ActiveAndEnabled)
                {
                    return;
                }

                if (dragScrolling)
                {
                    if (!vectorScrolling)
                    {
                        RegisterBaseEvents();
                    }

                    UIBlock.InputTarget.OnPointerInputChanged += HandlePointerInput;
                }
                else
                {
                    UIBlock.InputTarget.OnPointerInputChanged -= HandlePointerInput;

                    if (!vectorScrolling)
                    {
                        UnregisterBaseEvents();
                    }
                }
            }
        }

        public bool VectorScrolling
        {
            get => vectorScrolling;
            set
            {
                if (value == vectorScrolling)
                {
                    return;
                }

                vectorScrolling = value;
                if (!ActiveAndEnabled)
                {
                    return;
                }

                if (vectorScrolling)
                {
                    if (!dragScrolling)
                    {
                        RegisterBaseEvents();
                    }

                    UIBlock.InputTarget.OnVector3InputChanged += HandleScrollVector;
                }
                else
                {
                    UIBlock.InputTarget.OnVector3InputChanged -= HandleScrollVector;

                    if (!dragScrolling)
                    {
                        UnregisterBaseEvents();
                    }
                }
            }
        }

        public bool DraggableHorizontalScrollbar
        {
            get => draggableHorizontalScrollbar;
            set
            {
                if (value == draggableHorizontalScrollbar)
                {
                    return;
                }

                draggableHorizontalScrollbar = value;
                RefreshScrollbarHandlers(0);
            }
        }

        public bool DraggableVerticalScrollbar
        {
            get => draggableVerticalScrollbar;
            set
            {
                if (value == draggableVerticalScrollbar)
                {
                    return;
                }

                draggableVerticalScrollbar = value;
                RefreshScrollbarHandlers(1);
            }
        }

        public void CancelScroll()
        {
            if (!ActiveAndEnabled)
            {
                return;
            }

            if (latestSource.Initialized)
            {
                UIBlock.InputTarget.CancelInput(latestSource);
            }
            else
            {
                Canceled(ref latestSource);
            }
        }

        /// <summary>
        /// Scrolls content along enabled axes (viewport local space, same units as pointer drag).
        /// </summary>
        public void Scroll(Vector2 delta)
        {
            if (!ActiveAndEnabled)
            {
                return;
            }

            Vector3 v = Vector3.zero;
            if (ScrollHorizontal)
            {
                v.x = delta.x;
            }

            if (ScrollVertical)
            {
                v.y = delta.y;
            }

            Internal.Interaction source = Internal.Interaction.Uninitialized;
            VectorScroll(v, ref source);
        }

        /// <summary>
        /// Moves the horizontal scrollbar thumb and scrolls content accordingly (same contract as <see cref="Scroller.DragScrollbarToPosition"/>).
        /// </summary>
        public void DragHorizontalScrollbarToPosition(Vector3 newScrollbarWorldPosition)
        {
            DragScrollbarToPosition(newScrollbarWorldPosition, 0, horizontalScrollbar);
        }

        /// <summary>
        /// Moves the vertical scrollbar thumb and scrolls content accordingly (same contract as <see cref="Scroller.DragScrollbarToPosition"/>).
        /// </summary>
        public void DragVerticalScrollbarToPosition(Vector3 newScrollbarWorldPosition)
        {
            DragScrollbarToPosition(newScrollbarWorldPosition, 1, verticalScrollbar);
        }

        #endregion

        #region Internal state

        private sealed class AxisBounds : IScrollBoundsProvider
        {
            private readonly ScrollBlock owner;
            private readonly int axisIndex;

            public AxisBounds(ScrollBlock owner, int axisIndex)
            {
                this.owner = owner;
                this.axisIndex = axisIndex;
            }

            public ScrollBounds GetBounds() => owner.ComputeBounds(axisIndex);
        }

        /// <summary>
        /// Read-only viewport for measurements: padded size vs <see cref="content"/> extent (scrollbar thumb scales inversely). Defaults to <see cref="content"/>’s parent <see cref="UIBlock"/> unless <see cref="scrollViewportOverride"/> is set. Not mutated.
        /// </summary>
        private UIBlock ScrollViewport
        {
            get
            {
                if (scrollViewportOverride != null)
                {
                    return scrollViewportOverride;
                }

                if (content != null && content.Parent is UIBlock parentBlock)
                {
                    return parentBlock;
                }

                return UIBlock;
            }
        }

        private float scrollBasisX;
        private float scrollBasisY;

        private ScrollBehavior scrollBehaviorX;
        private ScrollBehavior scrollBehaviorY;

        private ScrollBehavior BehaviorX => scrollBehaviorX ??= new ScrollBehavior(boundsX);
        private ScrollBehavior BehaviorY => scrollBehaviorY ??= new ScrollBehavior(boundsY);

        private readonly AxisBounds boundsX;
        private readonly AxisBounds boundsY;

        private SimpleMovingAverage velocityTrackerX;
        private SimpleMovingAverage velocityTrackerY;
        private SimpleMovingAverage dragTrackerX;
        private SimpleMovingAverage dragTrackerY;

        private float scrollEndTime;
        private float currentTime;

        private bool decelerateX = true;
        private bool decelerateY = true;

        private float totalScrollThisFrameX;
        private float totalScrollThisFrameY;

        private bool immediateScrolled;

        /// <summary>
        /// Cached per-axis content extent for scroll bounds (see <see cref="GetScrollableContentSize"/>). Sources match <see cref="ReadLiveScrollExtentAlongAxis"/>; updated when idle so gesture frames do not chase twitchy layout.
        /// </summary>
        private float stableScrollContentExtentX;
        private float stableScrollContentExtentY;

        private float capturedRestingScrollBasisX;
        private float capturedRestingScrollBasisY;
        private bool restingScrollBasisCaptured;

        /// <summary>
        /// Values beyond this on scroll-related sizes (or scroll basis / laid-out position magnitude) are treated as corrupt so overscroll bugs surface as a single actionable console error with diagnostics.
        /// </summary>
        private const float ScrollMetricsSanityLimit = 10000f;

        private bool scrollMetricsCorruptionLogged;

        private Internal.Interaction latestSource;

        private RawInput.VectorInputChangeEvent HandleScrollVector =>
            handleScrollVector ??= HandleScroll;

        private RawInput.VectorInputChangeEvent handleScrollVector;

        private UIEventHandler<Gesture.OnDrag> handleHScrollbarDrag;
        private UIEventHandler<Gesture.OnRelease> handleHScrollbarRelease;
        private UIEventHandler<Gesture.OnDrag> handleVScrollbarDrag;
        private UIEventHandler<Gesture.OnRelease> handleVScrollbarRelease;

        private UIEventHandler<Gesture.OnDrag> HandleHScrollbarDrag =>
            handleHScrollbarDrag ??= HandleHorizontalScrollbar;

        private UIEventHandler<Gesture.OnRelease> HandleHScrollbarRelease =>
            handleHScrollbarRelease ??= HandleHorizontalScrollbar;

        private UIEventHandler<Gesture.OnDrag> HandleVScrollbarDrag =>
            handleVScrollbarDrag ??= HandleVerticalScrollbar;

        private UIEventHandler<Gesture.OnRelease> HandleVScrollbarRelease =>
            handleVScrollbarRelease ??= HandleVerticalScrollbar;

        System.Type IEventTargetProvider.BaseTargetableType => typeof(UIBlock);

        bool IEventTargetProvider.TryGetTarget(IEventTarget receiver, System.Type _, out IEventTarget target)
        {
            target = UIBlock;
            return true;
        }

        private ScrollBlock()
        {
            boundsX = new AxisBounds(this, 0);
            boundsY = new AxisBounds(this, 1);
            ClickBehavior = ClickBehavior.None;
            onSelect = SelectBehavior.ScopeNavigation;
        }

        #endregion

        #region Lifecycle

        private protected override void Init()
        {
            BehaviorX.Reset();
            BehaviorY.Reset();

            velocityTrackerX = new SimpleMovingAverage(0);
            velocityTrackerY = new SimpleMovingAverage(0);
            dragTrackerX = new SimpleMovingAverage(0);
            dragTrackerY = new SimpleMovingAverage(0);

            totalScrollThisFrameX = 0;
            totalScrollThisFrameY = 0;

            if (DragScrolling || VectorScrolling)
            {
                RegisterBaseEvents();
            }

            if (VectorScrolling)
            {
                UIBlock.InputTarget.OnVector3InputChanged += HandleScrollVector;
            }

            if (DragScrolling)
            {
                UIBlock.InputTarget.OnPointerInputChanged += HandlePointerInput;
            }

            UIBlock.InputTarget.SetNavigationNode(this);

            RefreshScrollbarHandlers(0);
            RefreshScrollbarHandlers(1);

            // Stable extents must exist before any basis math — GetScrollableContentSize feeds RefreshBasis; zero size corrupts alignment conversion.
            InitializeStableScrollExtentsFromLiveLayout();
            CaptureScrollLayoutOrigin();
            RefreshBasis();
        }

        private protected override void Deinit()
        {
            if (DragScrolling || VectorScrolling)
            {
                UnregisterBaseEvents();
            }

            if (VectorScrolling)
            {
                UIBlock.InputTarget.OnVector3InputChanged -= HandleScrollVector;
            }

            if (DragScrolling)
            {
                UIBlock.InputTarget.OnPointerInputChanged -= HandlePointerInput;
            }

            UIBlock.InputTarget.ClearNavigationNode();

            RemoveScrollbarHandlers();

            latestSource = default;
            RefreshBasis();
        }

        private void RegisterBaseEvents()
        {
            UIBlock.RegisterEventTargetProvider(this);
            UIBlock.InputTarget.SetGestureRecognizer(this);
            UIBlock.InputTarget.OnInputCanceled += InputCanceledHandler;
        }

        private void UnregisterBaseEvents()
        {
            UIBlock.UnregisterEventTargetProvider(this);
            UIBlock.InputTarget.ClearGestureRecognizer();
            UIBlock.InputTarget.OnInputCanceled -= InputCanceledHandler;
        }

        private void LateUpdate()
        {
            // Promote extents from live layout before scroll math when the cache is still zero — gesture freeze / dirty layout otherwise never fills stable, and transient LayoutSize=0 corrupts bounds.
            BootstrapStableScrollExtentsWhenUnset();

            bool clampElastic = OverscrollEffect == OverscrollEffect.Clamp;
            BehaviorX.ClampToBounds = clampElastic;
            BehaviorY.ClampToBounds = clampElastic;

            float nx = scrollBasisX;
            float ny = scrollBasisY;

            if (ScrollHorizontal)
            {
                if (decelerateX)
                {
                    nx = BehaviorX.AutoUpdate(currentTime);
                }
                else
                {
                    float scroll = totalScrollThisFrameX;
                    float viewport = ScrollViewport.PaddedSize[0] * 0.5f;
                    dragTrackerX.AddSample(Math.Clamp(scroll, -viewport, viewport));
                    float delta = immediateScrolled ? scroll : dragTrackerX.Value;
                    velocityTrackerX.AddSample(delta / Time.unscaledDeltaTime);
                    nx = BehaviorX.ManualUpdate(delta, currentTime);
                }
            }

            if (ScrollVertical)
            {
                if (decelerateY)
                {
                    ny = BehaviorY.AutoUpdate(currentTime);
                }
                else
                {
                    float scroll = totalScrollThisFrameY;
                    float viewport = ScrollViewport.PaddedSize[1] * 0.5f;
                    dragTrackerY.AddSample(Math.Clamp(scroll, -viewport, viewport));
                    float delta = immediateScrolled ? scroll : dragTrackerY.Value;
                    velocityTrackerY.AddSample(delta / Time.unscaledDeltaTime);
                    ny = BehaviorY.ManualUpdate(delta, currentTime);
                }
            }

            float snapshotBasisX = scrollBasisX;
            float snapshotBasisY = scrollBasisY;

            ScrollAxisTo(0, nx, snapshotBasisX);
            ScrollAxisTo(1, ny, snapshotBasisY);

            if (UIBlock.LayoutIsDirty || ScrollViewport.LayoutIsDirty || ScrollbarContentDirty())
            {
                SyncScrollbar(0, horizontalScrollbar);
                SyncScrollbar(1, verticalScrollbar);
            }

            ReportCorruptedScrollMetricsIfNeeded();

            currentTime += Time.unscaledDeltaTime;
            totalScrollThisFrameX = 0;
            totalScrollThisFrameY = 0;

            // Run after scroll math so extent does not change mid-frame (Clamp release / layout settle used to spike bounds).
            MaybeRefreshStableScrollExtentsFromLiveLayout();
        }

        #endregion

        #region Scroll math


        private bool AxisBasisPastStrictRange(int axisIndex)
        {
            if (content == null)
            {
                return false;
            }

            ScrollBounds bounds = ComputeBounds(axisIndex);
            float pos = axisIndex == 0 ? scrollBasisX : scrollBasisY;
            var withPos = new ScrollBounds(bounds.MinMax, pos, bounds.ViewportDimension);
            return withPos.OutOfRange;
        }

        /// <summary>
        /// Scroll extent for one axis: prefers invariant footprint (<see cref="UIBlock.LayoutSize"/>, <see cref="UIBlock.CalculatedSize"/>) over <see cref="UIBlock.ContentSize"/> so elastic overscroll does not inflate bounds.
        /// Uses a cached stable value while scrolling so extent does not twitch mid-gesture.
        /// </summary>
        private float GetScrollableContentSize(int axisIndex)
        {
            if (content == null)
            {
                return 0f;
            }

            float stable = axisIndex == 0 ? stableScrollContentExtentX : stableScrollContentExtentY;
            float live = ReadLiveScrollExtentAlongAxis(content, axisIndex);

            // Live extent can read 0 while Nova recomputes (dirty content); keep last stable extent so bounds / basis do not see a fake zero footprint.
            if (live <= 1e-6f && stable > 1e-6f)
            {
                return stable;
            }

            // Until stable cache is filled (or after reset), follow live (see ReadLiveScrollExtentAlongAxis).
            if (stable <= 1e-6f)
            {
                return live;
            }

            return stable;
        }

        /// <summary>
        /// Layout footprint for scroll extent: invariant to scroll offset. Prefer <see cref="UIBlock.LayoutSize"/>, then <see cref="UIBlock.CalculatedSize"/>; <see cref="UIBlock.ContentSize"/> last — aggregate bounds can include elastic displacement during bounce.
        /// </summary>
        private static float ReadLiveScrollExtentAlongAxis(UIBlock contentRoot, int axisIndex)
        {
            float layout = contentRoot.LayoutSize[axisIndex];
            if (layout > 1e-6f)
            {
                return layout;
            }

            float calculated = contentRoot.CalculatedSize[axisIndex].Value;
            if (calculated > 1e-6f)
            {
                return calculated;
            }

            float aggregate = contentRoot.ContentSize[axisIndex];
            if (aggregate > 1e-6f)
            {
                return aggregate;
            }

            return 0f;
        }

        /// <summary>
        /// Fills stable extents when still zero while live layout reports a real footprint (via <see cref="ReadLiveScrollExtentAlongAxis"/>). Runs even when <see cref="ShouldFreezeStableScrollExtents"/> would block the periodic refresh.
        /// </summary>
        private void BootstrapStableScrollExtentsWhenUnset()
        {
            if (content == null)
            {
                return;
            }

            bool updated = false;

            if (stableScrollContentExtentX <= 1e-6f)
            {
                float liveX = ReadLiveScrollExtentAlongAxis(content, 0);
                if (liveX > 1e-6f)
                {
                    stableScrollContentExtentX = liveX;
                    updated = true;
                }
            }

            if (stableScrollContentExtentY <= 1e-6f)
            {
                float liveY = ReadLiveScrollExtentAlongAxis(content, 1);
                if (liveY > 1e-6f)
                {
                    stableScrollContentExtentY = liveY;
                    updated = true;
                }
            }

            if (updated)
            {
                RefreshBasis();
            }
        }

        private void InitializeStableScrollExtentsFromLiveLayout()
        {
            if (content == null)
            {
                stableScrollContentExtentX = 0f;
                stableScrollContentExtentY = 0f;
                return;
            }

            stableScrollContentExtentX = ReadLiveScrollExtentAlongAxis(content, 0);
            stableScrollContentExtentY = ReadLiveScrollExtentAlongAxis(content, 1);
        }

        /// <summary>
        /// Copies live invariant footprint into stable extents when idle and layout has settled. Skips refresh during bounce elastic stretch so the cache is not polluted. Updating extent before <see cref="LateUpdate"/> scroll steps made bounds jump on finger release (Clamp) while Nova was still recomputing layout.
        /// </summary>
        private void MaybeRefreshStableScrollExtentsFromLiveLayout()
        {
            if (content == null)
            {
                return;
            }

            if (ShouldFreezeStableScrollExtents())
            {
                return;
            }

            if (!IsLayoutSettledForStableExtentRefresh())
            {
                return;
            }

            if (OverscrollEffect == OverscrollEffect.Bounce)
            {
                if (AxisBasisPastStrictRange(0) || AxisBasisPastStrictRange(1))
                {
                    return;
                }
            }

            float liveX = ReadLiveScrollExtentAlongAxis(content, 0);
            float liveY = ReadLiveScrollExtentAlongAxis(content, 1);
            bool changed = math.abs(liveX - stableScrollContentExtentX) > 1e-4f ||
                           math.abs(liveY - stableScrollContentExtentY) > 1e-4f;

            stableScrollContentExtentX = liveX;
            stableScrollContentExtentY = liveY;

            if (changed)
            {
                RefreshBasis();
            }
        }

        /// <summary>
        /// Avoid sampling <see cref="UIBlock.LayoutSize"/> while layout is dirty — sizes are intermediate and drive bogus bounds/thumb math for one frame.
        /// </summary>
        private bool IsLayoutSettledForStableExtentRefresh()
        {
            if (UIBlock.LayoutIsDirty || ScrollViewport.LayoutIsDirty || content.LayoutIsDirty)
            {
                return false;
            }

            return true;
        }

        private bool ShouldFreezeStableScrollExtents()
        {
            if (math.abs(totalScrollThisFrameX) > 1e-6f || math.abs(totalScrollThisFrameY) > 1e-6f || immediateScrolled)
            {
                return true;
            }

            if (!decelerateX || !decelerateY)
            {
                return true;
            }

            return IsMoving();
        }

        /// <summary>
        /// Overscroll / layout ordering bugs can blow up sizes or scroll state. Log once per corrupt streak with full measurements so the scene can be fixed without silent failure.
        /// </summary>
        private void ReportCorruptedScrollMetricsIfNeeded()
        {
            if (content == null)
            {
                return;
            }

            if (!TryBuildScrollMetricsCorruptionSummary(out string triggers))
            {
                scrollMetricsCorruptionLogged = false;
                return;
            }

            if (!scrollMetricsCorruptionLogged)
            {
                Debug.LogError(BuildScrollMetricsDiagnosticReport(triggers));
                scrollMetricsCorruptionLogged = true;
            }
        }

        private static bool AxisScalarSizeOutOfSanity(float value)
        {
            return value < 0f || value > ScrollMetricsSanityLimit;
        }

        private static bool Vector3AnyAxisSizeOutOfSanity(Vector3 v)
        {
            return AxisScalarSizeOutOfSanity(v.x) || AxisScalarSizeOutOfSanity(v.y) || AxisScalarSizeOutOfSanity(v.z);
        }

        private bool TryBuildScrollMetricsCorruptionSummary(out string triggers)
        {
            triggers = string.Empty;

            if (Vector3AnyAxisSizeOutOfSanity(content.LayoutSize))
            {
                triggers += "content.LayoutSize";
            }

            if (Vector3AnyAxisSizeOutOfSanity(content.ContentSize))
            {
                if (triggers.Length > 0)
                {
                    triggers += ", ";
                }

                triggers += "content.ContentSize";
            }

            if (AxisScalarSizeOutOfSanity(stableScrollContentExtentX) || AxisScalarSizeOutOfSanity(stableScrollContentExtentY))
            {
                if (triggers.Length > 0)
                {
                    triggers += ", ";
                }

                triggers += "stableScrollContentExtent";
            }

            Vector3 viewportPad = ScrollViewport.PaddedSize;
            if (Vector3AnyAxisSizeOutOfSanity(viewportPad))
            {
                if (triggers.Length > 0)
                {
                    triggers += ", ";
                }

                triggers += "viewport.PaddedSize";
            }

            if (math.abs(scrollBasisX) > ScrollMetricsSanityLimit || math.abs(scrollBasisY) > ScrollMetricsSanityLimit)
            {
                if (triggers.Length > 0)
                {
                    triggers += ", ";
                }

                triggers += "scrollBasis";
            }

            return triggers.Length > 0;
        }

        private string BuildScrollMetricsDiagnosticReport(string triggers)
        {
            ref Layout configuredLayout = ref Content.Layout;
            ref readonly Length3.Calculated calculatedPosition = ref Content.CalculatedPosition;
            UIBlock viewport = ScrollViewport;
            Vector3 viewportPad = viewport.PaddedSize;

            float extentForBoundsX = GetScrollableContentSize(0);
            float extentForBoundsY = GetScrollableContentSize(1);
            float liveExtentX = ReadLiveScrollExtentAlongAxis(content, 0);
            float liveExtentY = ReadLiveScrollExtentAlongAxis(content, 1);

            return
                $"Scroll Block '{gameObject.name}' scroll metrics look corrupt ({triggers}). " +
                $"OverscrollEffect={OverscrollEffect}, ScrollHorizontal={ScrollHorizontal}, ScrollVertical={ScrollVertical}, " +
                $"decelerateX={decelerateX}, decelerateY={decelerateY}, immediateScrolled={immediateScrolled}. " +
                $"totalScrollThisFrame=({totalScrollThisFrameX},{totalScrollThisFrameY}). " +
                $"scrollBasis=({scrollBasisX},{scrollBasisY}), layoutPosition XY=({configuredLayout.Position.X.Value},{configuredLayout.Position.Y.Value}), calculatedPosition XY=({calculatedPosition[0].Value},{calculatedPosition[1].Value}). " +
                $"Extent for bounds math (GetScrollableContentSize): XY=({extentForBoundsX:F2},{extentForBoundsY:F2}). Live read (LayoutSize→CalculatedSize→ContentSize fallback): XY=({liveExtentX:F2},{liveExtentY:F2}). Stable extent cache: XY=({stableScrollContentExtentX:F2},{stableScrollContentExtentY:F2}). " +
                $"Viewport scroll window (PaddedSize) XY=({viewportPad.x:F2},{viewportPad.y:F2}). " +
                $"content '{content.gameObject.name}' LayoutSize={content.LayoutSize}, ContentSize={content.ContentSize}. " +
                $"When those match the intended footprint (e.g. 840×182), treat sizes as OK — this error is scroll basis / layout offset drift, not wrong panel dimensions. " +
                $"Layout.Alignment=({(int)Content.Layout.Alignment.X},{(int)Content.Layout.Alignment.Y},{(int)Content.Layout.Alignment.Z}). " +
                $"viewport '{viewport.gameObject.name}' ContentCenter={viewport.ContentCenter}, LayoutIsDirty UIBlock={UIBlock.LayoutIsDirty} viewport={viewport.LayoutIsDirty} content={content.LayoutIsDirty}.";
        }

        private ScrollBounds ComputeBounds(int axisIndex)
        {
            if (content == null)
            {
                return default;
            }

            float scrollRange = ScrollViewport.PaddedSize[axisIndex];
            float contentSize = GetScrollableContentSize(axisIndex);
            float sizeDelta = scrollRange - contentSize;
            float basis = axisIndex == 0 ? scrollBasisX : scrollBasisY;

            if (sizeDelta < 0)
            {
                float scrollSpaceOffset = ScrollSpaceOffsetFromContentLayout(axisIndex);
                float virtualizedShift = basis - scrollSpaceOffset;

                double2 minMax;
                minMax.x = math.max(sizeDelta + virtualizedShift, -contentSize);
                minMax.y = math.min(virtualizedShift, contentSize);

                if (minMax.x > minMax.y)
                {
                    minMax = minMax.yx;
                }

                ApplyStrictScrollClampBuffer(ref minMax, axisIndex);

                return new ScrollBounds(minMax, basis, scrollRange);
            }

            int align = axisIndex == 0 ? (int)Content.Layout.Alignment.X :
                axisIndex == 1 ? (int)Content.Layout.Alignment.Y : (int)Content.Layout.Alignment.Z;

            float anchorScalar = (align - 1) * -0.5f;
            float anchor = sizeDelta * anchorScalar;

            return new ScrollBounds(math.double2(anchor, anchor), basis, scrollRange);
        }

        /// <summary>
        /// Pulls strict min/max toward each other so the native clamp range sits slightly inside Nova’s ContentSize vs viewport edge (ListView/Scroller-style robustness).
        /// </summary>
        private void ApplyStrictScrollClampBuffer(ref double2 minMax, int axisIndex)
        {
            double buffer = strictScrollClampBuffer + ScrollViewport.PaddedSize[axisIndex] * strictScrollClampBufferViewportFraction;
            if (buffer <= 0.0)
            {
                return;
            }

            double span = minMax.y - minMax.x;
            if (span <= 0.0)
            {
                return;
            }

            double maxInset = span * 0.5 - 1e-6;
            double inset = math.min(buffer, maxInset);
            minMax.x += inset;
            minMax.y -= inset;
            if (minMax.x > minMax.y)
            {
                double mid = (minMax.x + minMax.y) * 0.5;
                minMax.x = mid;
                minMax.y = mid;
            }
        }

        /// <summary>
        /// Scroll offset in alignment‑1 space implied by current <see cref="Content"/> <see cref="Layout.Position"/>, minus the resting unified basis captured at init (see <see cref="CaptureScrollLayoutOrigin"/>).
        /// </summary>
        private float ScrollSpaceOffsetFromContentLayout(int axisIndex)
        {
            return RelativeScrollBasisFromContentLayout(axisIndex);
        }

        /// <summary>
        /// Aligns <see cref="scrollBasisX"/> / Y with <see cref="Scroller"/>: unified scroll basis minus resting capture.
        /// </summary>
        private void RefreshBasis()
        {
            if (content == null)
            {
                return;
            }

            scrollBasisX = RelativeScrollBasisFromContentLayout(0);
            scrollBasisY = RelativeScrollBasisFromContentLayout(1);
        }

        /// <summary>
        /// Configured <see cref="Layout.Position"/> on <see cref="content"/> — the same values <see cref="ScrollAxisTo"/> updates. Matches Nova’s <see cref="Scroller"/> using <see cref="AutoLayout.Offset"/> rather than lagging calculated layout output.
        /// </summary>
        private float ReadContentScrollDriveLayoutOffset(int axisIndex)
        {
            ref Layout layout = ref Content.Layout;
            return axisIndex switch
            {
                0 => layout.Position.X.Value,
                1 => layout.Position.Y.Value,
                _ => layout.Position.Z.Value,
            };
        }

        /// <summary>
        /// Maps authored layout <see cref="Layout.Position"/> on an axis to Nova’s alignment‑1 scroll basis (same path as <see cref="Scroller.RefreshBasis"/>).
        /// </summary>
        private float LayoutPositionToScrollBasis(int axisIndex, float layoutPositionOnAxis)
        {
            float scrollRange = ScrollViewport.PaddedSize[axisIndex];
            float contentSize = math.max(GetScrollableContentSize(axisIndex), 1e-4f);
            float spacingOffset = ScrollViewport.CalculatedPadding.Offset[axisIndex] + Content.CalculatedMargin.Offset[axisIndex];
            int alignment = axisIndex == 0 ? (int)Content.Layout.Alignment.X :
                axisIndex == 1 ? (int)Content.Layout.Alignment.Y : (int)Content.Layout.Alignment.Z;

            if (alignment == 1)
            {
                return layoutPositionOnAxis;
            }

            float localPosition = LayoutUtils.LayoutOffsetToLocalPosition(layoutPositionOnAxis, contentSize, scrollRange, spacingOffset, alignment);
            return LayoutUtils.LocalPositionToLayoutOffset(localPosition, contentSize, scrollRange, spacingOffset, 1);
        }

        /// <summary>
        /// Unified scroll basis from the current layout pose, minus the resting basis captured at init so zero scroll matches authored alignment at startup.
        /// </summary>
        private float RelativeScrollBasisFromContentLayout(int axisIndex)
        {
            float absoluteBasis = LayoutPositionToScrollBasis(axisIndex, ReadContentScrollDriveLayoutOffset(axisIndex));
            if (!restingScrollBasisCaptured)
            {
                return absoluteBasis;
            }

            float origin = axisIndex == 0 ? capturedRestingScrollBasisX : capturedRestingScrollBasisY;
            return absoluteBasis - origin;
        }

        /// <summary>
        /// Records unified scroll basis at startup (after extents init). Raw layout position minus this gives incorrect scroll zero — subtraction happens after <see cref="LayoutPositionToScrollBasis"/>.
        /// </summary>
        private void CaptureScrollLayoutOrigin()
        {
            if (content == null)
            {
                restingScrollBasisCaptured = false;
                return;
            }

            capturedRestingScrollBasisX = LayoutPositionToScrollBasis(0, ReadContentScrollDriveLayoutOffset(0));
            capturedRestingScrollBasisY = LayoutPositionToScrollBasis(1, ReadContentScrollDriveLayoutOffset(1));
            restingScrollBasisCaptured = true;
        }

        /// <summary>
        /// Same sign convention as <see cref="AutoLayout.AlignmentPositiveDirection"/> on Nova’s single-axis <see cref="Scroller"/> — unified scroll delta maps onto <see cref="Layout.Position"/> values.
        /// </summary>
        private static int AxisScrollAlignmentPositiveDirection(int alignment)
        {
            return alignment == 1 ? -1 : 1;
        }

        /// <summary>
        /// Applies one axis of scroll output by updating content layout; <paramref name="basisSnapshotAtFrameStart"/> must match pre-frame basis so X/Y stays consistent when both axes run in one <see cref="LateUpdate"/>.
        /// </summary>
        /// <param name="basisSnapshotAtFrameStart">
        /// Scroll basis for this axis before any axis writes layout this frame (same snapshot <see cref="ScrollBehavior.ManualUpdate"/> used).
        /// </param>
        private void ScrollAxisTo(int axis, float newBasis, float basisSnapshotAtFrameStart)
        {
            // Same contract as Scroller.ScrollTo: delta = scrollBasis - newPosition; layout.Offset += delta * AlignmentPositiveDirection; RefreshBasis().
            float delta = basisSnapshotAtFrameStart - newBasis;

            if (axis == 0 && !ScrollHorizontal)
            {
                return;
            }

            if (axis == 1 && !ScrollVertical)
            {
                return;
            }

            if (decelerateX && axis == 0)
            {
                velocityTrackerX.Value = BehaviorX.GetSimulationVelocity(currentTime);
            }

            if (decelerateY && axis == 1)
            {
                velocityTrackerY.Value = BehaviorY.GetSimulationVelocity(currentTime);
            }

            if (!float.IsFinite(delta) || Math.ApproximatelyZero(delta))
            {
                return;
            }

            float viewportSize = ScrollViewport.PaddedSize[axis];
            delta = Math.MinAbs(delta, 2f * viewportSize * Mathf.Sign(delta));

            int layoutAlignment = axis == 0 ? (int)Content.Layout.Alignment.X : (int)Content.Layout.Alignment.Y;
            float layoutDelta = delta * AxisScrollAlignmentPositiveDirection(layoutAlignment);

            ref Layout layout = ref Content.Layout;
            if (axis == 0)
            {
                Length lx = layout.Position.X;
                lx.Value += layoutDelta;
                layout.Position.X = lx;
            }
            else
            {
                Length ly = layout.Position.Y;
                ly.Value += layoutDelta;
                layout.Position.Y = ly;
            }

            RefreshBasis();

            Vector3 scrollDelta = Vector3.zero;
            scrollDelta[axis] = delta;
            ScrollType scrollType = (axis == 0 ? decelerateX : decelerateY) ? ScrollType.Inertial : ScrollType.Manual;
            UIBlock.FireEvent(Gesture.Scroll(latestSource.ToPublic(), UIBlock, scrollType, scrollDelta));
        }

        #endregion

        #region Scrollbars

        private static bool AxisEnabled(UIBlock scrollbar)
        {
            return scrollbar != null && scrollbar.gameObject.activeInHierarchy;
        }

        private void RefreshScrollbarHandlers(int axis)
        {
            UIBlock sb = axis == 0 ? horizontalScrollbar : verticalScrollbar;
            bool want = axis == 0 ? draggableHorizontalScrollbar : draggableVerticalScrollbar;

            if (sb == null || !ActiveAndEnabled)
            {
                return;
            }

            sb.RemoveEventHandler(axis == 0 ? HandleHScrollbarRelease : HandleVScrollbarRelease);
            sb.RemoveEventHandler(axis == 0 ? HandleHScrollbarDrag : HandleVScrollbarDrag);

            if (want)
            {
                EnsureScrollbarThumbDraggableForAxis(sb, axis);
                sb.AddEventHandler(axis == 0 ? HandleHScrollbarRelease : HandleVScrollbarRelease, includeHierarchy: true);
                sb.AddEventHandler(axis == 0 ? HandleHScrollbarDrag : HandleVScrollbarDrag, includeHierarchy: true);
            }
        }

        /// <summary>
        /// Nova only emits <see cref="Gesture.OnDrag"/> (and non-zero <see cref="Gesture.OnDrag.DragDeltaWorldSpace"/>) when the thumb’s
        /// <see cref="Interactable"/>.<see cref="Interactable.Draggable"/> is true on that axis. Vertical list thumbs often ship with Y only; horizontal needs X.
        /// </summary>
        private static void EnsureScrollbarThumbDraggableForAxis(UIBlock thumb, int axisIndex)
        {
            if (thumb == null)
            {
                return;
            }

            Interactable interactable = thumb.GetComponent<Interactable>();
            if (interactable == null)
            {
                interactable = thumb.gameObject.AddComponent<Interactable>();
            }

            ThreeD<bool> d = interactable.Draggable;
            if (axisIndex == 0)
            {
                d.X = true;
                d.Y = false;
                d.Z = false;
            }
            else
            {
                d.X = false;
                d.Y = true;
                d.Z = false;
            }

            interactable.Draggable = d;
        }

        private void RemoveScrollbarHandlers()
        {
            if (horizontalScrollbar != null)
            {
                horizontalScrollbar.RemoveEventHandler(HandleHScrollbarRelease);
                horizontalScrollbar.RemoveEventHandler(HandleHScrollbarDrag);
            }

            if (verticalScrollbar != null)
            {
                verticalScrollbar.RemoveEventHandler(HandleVScrollbarRelease);
                verticalScrollbar.RemoveEventHandler(HandleVScrollbarDrag);
            }
        }

        private void HandleHorizontalScrollbar(Gesture.OnDrag evt)
        {
            DragHorizontalScrollbarToPosition(horizontalScrollbar.transform.position + evt.DragDeltaWorldSpace);
            immediateScrolled = false;
        }

        private void HandleHorizontalScrollbar(Gesture.OnRelease evt)
        {
            Internal.Interaction source = evt.Interaction.ToInternal();
            Canceled(ref source);
        }

        private void HandleVerticalScrollbar(Gesture.OnDrag evt)
        {
            DragVerticalScrollbarToPosition(verticalScrollbar.transform.position + evt.DragDeltaWorldSpace);
            immediateScrolled = false;
        }

        private void HandleVerticalScrollbar(Gesture.OnRelease evt)
        {
            Internal.Interaction source = evt.Interaction.ToInternal();
            Canceled(ref source);
        }

        private void DragScrollbarToPosition(Vector3 newScrollbarWorldPosition, int axisIndex, UIBlock scrollbarVisual)
        {
            if (!ActiveAndEnabled || scrollbarVisual == null || scrollbarVisual.Parent == null || !AxisEnabledForAxis(axisIndex))
            {
                return;
            }

            if (axisIndex == 0)
            {
                decelerateX = false;
            }
            else
            {
                decelerateY = false;
            }

            Vector3 newScrollbarPositionLocalSpace = scrollbarVisual.transform.parent.InverseTransformPoint(newScrollbarWorldPosition);
            float scrollableSize = scrollbarVisual.Parent.PaddedSize[axisIndex];
            float dragDelta = newScrollbarPositionLocalSpace[axisIndex] - scrollbarVisual.transform.localPosition[axisIndex];

            Length.Calculated size = scrollbarVisual.CalculatedSize[axisIndex] + scrollbarVisual.CalculatedMargin[axisIndex].Sum();
            float range = math.max(0, 0.5f - (size.Percent * 0.5f));

            if (Math.ApproximatelyZero(range))
            {
                return;
            }

            float dragPercent = dragDelta / scrollableSize;
            // Same as Scroller.DragScrollbarToPosition: scrollBy = -dragPercent * contentSize along the axis.
            float scrollBy = -dragPercent * GetScrollableContentSize(axisIndex);

            if (axisIndex == 0)
            {
                totalScrollThisFrameX += scrollBy;
            }
            else
            {
                totalScrollThisFrameY += scrollBy;
            }

            latestSource = Internal.Interaction.Uninitialized;
        }

        private bool AxisEnabledForAxis(int axisIndex)
        {
            return axisIndex == 0 ? ScrollHorizontal : ScrollVertical;
        }

        private bool ScrollbarContentDirty()
        {
            if (content == null)
            {
                return false;
            }

            return previousContentExtentX != ReadLiveScrollExtentAlongAxis(content, 0) ||
                   previousContentExtentY != ReadLiveScrollExtentAlongAxis(content, 1) ||
                   previousScrollViewportCenterX != ScrollViewport.ContentCenter[0] ||
                   previousScrollViewportCenterY != ScrollViewport.ContentCenter[1];
        }

        private float previousContentExtentX;
        private float previousContentExtentY;
        private float previousScrollViewportCenterX;
        private float previousScrollViewportCenterY;

        private void SyncScrollbar(int axisIndex, UIBlock scrollbar)
        {
            if (scrollbar == null || content == null || !AxisEnabledForAxis(axisIndex))
            {
                return;
            }

            float viewportSize = ScrollViewport.CalculatedSize[axisIndex].Value;
            float scrollableSize = ScrollViewport.PaddedSize[axisIndex];
            float totalContentSize = GetScrollableContentSize(axisIndex);
            float contentOffset = ScrollViewport.ContentCenter[axisIndex] - ScrollViewport.CalculatedPadding.Offset[axisIndex];
            float position = -contentOffset;

            float overscroll = 0;

            float extent = totalContentSize * 0.5f;
            float minExtent = position - extent;
            float maxExtent = position + extent;

            AutoLayout layout = ScrollViewport.GetAutoLayoutReadOnly();
            overscroll = totalContentSize > scrollableSize ? maxExtent - scrollableSize * 0.5f : layout.Offset * -layout.AlignmentPositiveDirection;
            float underscroll = totalContentSize > scrollableSize ? (-scrollableSize * 0.5f) - minExtent : layout.Offset * layout.AlignmentPositiveDirection;
            float sizeAdjustment = math.min(overscroll, math.min(underscroll, 0));

            Length scrollbarSize = scrollbar.Size[axisIndex];
            float baseSize = math.max(math.max(totalContentSize, viewportSize), 1e-5f);
            float scrollbarPercent = (viewportSize + sizeAdjustment) / baseSize;
            scrollbarSize.Percent = math.clamp(scrollbarPercent, 0, 1 - math.max(scrollbar.CalculatedMargin[axisIndex].Sum().Percent, 0));
            scrollbar.Size[axisIndex] = scrollbarSize;

            UIBlock parent = scrollbar.Parent;
            float scrollbarParentSize = math.max(parent == null ? scrollableSize : parent.PaddedSize[axisIndex], 1e-5f);
            scrollbarPercent = scrollbar.SizeMinMax[axisIndex].Clamp(scrollbarSize.Percent * scrollbarParentSize) / scrollbarParentSize;

            if (!LayoutDataStore.Instance.HasReceivedFullEngineUpdate(scrollbar))
            {
                scrollbar.CalculateLayout();
            }

            float relativeSize = scrollbarPercent + math.min(scrollbar.CalculatedMargin[axisIndex].Sum().Percent, 0);
            Length scrollbarPosition = scrollbar.Position[axisIndex];
            float max = 1 - relativeSize;
            float range = 0.5f * max;

            // Same split as Internal.Scrollbar.UpdateVisuals; never mix in extent overscroll when travel is tiny — that caused thumb size/position spikes during elastic overscroll.
            float travel = totalContentSize - scrollableSize;
            float normalizedPosition;
            if (totalContentSize >= scrollableSize)
            {
                float denom = math.max(travel, 1e-5f);
                normalizedPosition = max * position / denom;
            }
            else
            {
                normalizedPosition = overscroll / math.max(scrollableSize, 1e-5f);
            }

            float clampedPosition = math.clamp(normalizedPosition, -range, range);

            scrollbarPosition.Percent = LayoutUtils.LocalPositionToLayoutOffset(clampedPosition, relativeSize, 1, 0, scrollbar.Alignment[axisIndex]);

            scrollbar.Position[axisIndex] = scrollbarPosition;

            previousContentExtentX = ReadLiveScrollExtentAlongAxis(content, 0);
            previousContentExtentY = ReadLiveScrollExtentAlongAxis(content, 1);
            previousScrollViewportCenterX = ScrollViewport.ContentCenter[0];
            previousScrollViewportCenterY = ScrollViewport.ContentCenter[1];
        }

        #endregion

        #region Input

        private void HandleScroll(ref RawInput.OnChanged<UniqueValue<Vector3>> evt)
        {
            if (!evt.Current.HasValue)
            {
                return;
            }

            Input<UniqueValue<Vector3>> scroll = evt.Current.Value;

            if (!scroll.IsHit || scroll.UserInput.Amount == Vector3.zero)
            {
                return;
            }

            Vector3 amount = scroll.UserInput.Amount * VectorScrollMultiplier;
            VectorScroll(amount, ref evt.Interaction);
        }

        private void VectorScroll(Vector3 delta, ref Internal.Interaction source)
        {
            immediateScrolled = true;
            AddScrollAmount(delta, ref source);
        }

        private void AddScrollAmount(Vector3 scroll, ref Internal.Interaction source)
        {
            if (ScrollHorizontal)
            {
                decelerateX = false;
                totalScrollThisFrameX += scroll.x;
            }

            if (ScrollVertical)
            {
                decelerateY = false;
                totalScrollThisFrameY += scroll.y;
            }

            latestSource = source;
        }

        private protected override void ProcessUniqueGesture(ref RawInput.OnChanged<bool> evt, ref InputState inputState, ref Vector3 currentPositionRootSpace, ref Matrix4x4 rootToWorld)
        {
            if (latestSource.Initialized && evt.Interaction.ID != latestSource.ID && InputStates.TryGetValue(latestSource.ID, out InputState previousState) && previousState.Dragged)
            {
                return;
            }

            Input<bool> previous = evt.Previous.GetValueOrDefault();
            Input<bool> current = evt.Current.GetValueOrDefault();

            bool wasActive = previous.UserInput;
            bool isActive = current.UserInput;

            bool started = current.GestureDetected && !previous.GestureDetected;
            bool gesturing = current.GestureDetected || previous.GestureDetected;

            if (!wasActive && isActive)
            {
                Canceled(ref evt.Interaction);
            }

            if (!gesturing || (!DragScrolling))
            {
                return;
            }

            inputState.Dragged = true;

            immediateScrolled = false;

            Vector3 delta = Vector3.zero;

            float threshold = math.abs(math.sin(math.radians(GetDragThreshold(ref current)))) * Vector3.Distance(evt.Interaction.Ray.origin, current.HitPoint);

            Vector3 previousWorldPos = evt.Previous.HasValue ? rootToWorld.MultiplyPoint(inputState.PreviousPositionRootSpace) : current.HitPoint;

            if (started)
            {
                RefreshBasis();
                BehaviorX.Start(currentTime - scrollEndTime, threshold);
                BehaviorY.Start(currentTime - scrollEndTime, threshold);
                dragTrackerX.Value = 0;
                dragTrackerY.Value = 0;

                if (wasActive)
                {
                    delta = evt.GetHitLocalTranslation(previousWorldPos);
                }
            }
            else
            {
                delta = evt.GetHitLocalTranslation(previousWorldPos);
            }

            if (!isActive)
            {
                return;
            }

            Vector3 masked = Vector3.zero;
            if (ScrollHorizontal)
            {
                masked.x = delta.x;
            }

            if (ScrollVertical)
            {
                masked.y = delta.y;
            }

            AddScrollAmount(masked, ref evt.Interaction);
        }

        private protected override bool Cancelable => IsMoving();

        private protected override void Canceled(ref Internal.Interaction source)
        {
            decelerateX = true;
            decelerateY = true;
            scrollEndTime = currentTime;

            RefreshBasis();

            BehaviorX.Cancel(scrollEndTime);
            BehaviorY.Cancel(scrollEndTime);

            immediateScrolled = false;
            totalScrollThisFrameX = 0;
            totalScrollThisFrameY = 0;

            velocityTrackerX.Value = 0;
            velocityTrackerY.Value = 0;
        }

        private protected override void Released(ref InputState state, ref Gesture.OnRelease evt)
        {
            if (decelerateX && decelerateY)
            {
                return;
            }

            scrollEndTime = currentTime;

            if (!decelerateX)
            {
                decelerateX = true;
                BehaviorX.End(velocityTrackerX.Value, scrollEndTime);
            }

            if (!decelerateY)
            {
                decelerateY = true;
                BehaviorY.End(velocityTrackerY.Value, scrollEndTime);
            }

            latestSource = evt.Interaction.ToInternal();
        }

        private bool IsMoving()
        {
            double maxVx = 0.1f * math.pow(10, math.round(math.log10(ScrollViewport.CalculatedSize[0].Value)));
            double maxVy = 0.1f * math.pow(10, math.round(math.log10(ScrollViewport.CalculatedSize[1].Value)));

            bool vx = !Math.ApproximatelyZero(velocityTrackerX.Value, epsilon: (float)maxVx);
            bool vy = !Math.ApproximatelyZero(velocityTrackerY.Value, epsilon: (float)maxVy);
            return vx || vy;
        }

        private float GetDragThreshold<T>(ref Input<T> input) where T : unmanaged, System.IEquatable<T> =>
            input.Noisy ? LowAccuracyDragThreshold : DragThreshold;

        GestureState IGestureRecognizer.TryRecognizeGesture<T>(Ray startRay, Input<T> start, Input<T> sample, Transform top)
        {
            GestureState state = GestureState.None;

            if (!ScrollHorizontal && !ScrollVertical)
            {
                return state;
            }

            if (IsMoving() && top.IsChildOf(transform))
            {
                return GestureState.Occurring;
            }

            state = GestureState.Pending;

            System.Type inputType = typeof(T);

            if (inputType == typeof(bool))
            {
                Vector3 startPosition = UIBlock.transform.InverseTransformPoint(start.HitPoint);
                Vector3 samplePosition = UIBlock.transform.InverseTransformPoint(sample.HitPoint);

                float dx = ScrollHorizontal ? Mathf.Abs(startPosition.x - samplePosition.x) : 0f;
                float dy = ScrollVertical ? Mathf.Abs(startPosition.y - samplePosition.y) : 0f;

                int axisHint = dx >= dy ? 0 : 1;
                if (axisHint == 0 && !ScrollHorizontal)
                {
                    axisHint = 1;
                }

                if (axisHint == 1 && !ScrollVertical)
                {
                    axisHint = 0;
                }

                Vector3 scrollAxis = Vector3.zero;
                scrollAxis[axisHint] = 1f;

                Vector3 scrollAxisWorldSpace = UIBlock.transform.TransformDirection(scrollAxis);
                Vector3 gestureNormal = startRay.direction;
                Vector3 gestureStartPoint = startRay.origin;
                Vector3 axis = Vector3.Cross(gestureNormal, scrollAxisWorldSpace);

                if (axis == Vector3.zero)
                {
                    scrollAxis = axisHint == 0 ? Vector3.right : Vector3.up;
                    gestureNormal = UIBlock.transform.TransformDirection(scrollAxis);
                    gestureStartPoint = start.HitPoint;
                    axis = Vector3.Cross(gestureNormal, scrollAxisWorldSpace);
                }

                Vector3 translationDirection = Math.ApproximatelyZeroToZero(sample.HitPoint - gestureStartPoint);

                float degrees = Math.AngleBetweenAroundAxis(gestureNormal, translationDirection, axis);
                degrees = math.min(degrees, 180 - degrees);

                state = degrees >= GetDragThreshold(ref sample)
                    ? GetDetectionPriority(startPosition, samplePosition)
                    : state;
            }
            else if (inputType == typeof(UniqueValue<Vector3>))
            {
                _ = sample.TryConvertIfSameType(out Input<UniqueValue<Vector3>>? scrollSample);
                Vector3 amount = scrollSample.Value.UserInput.Amount;

                bool any = (ScrollHorizontal && !Mathf.Approximately(amount.x, 0f)) ||
                           (ScrollVertical && !Mathf.Approximately(amount.y, 0f));

                state = any ? GestureState.DetectedHighPri : GestureState.None;
            }

            return state;
        }

        private GestureState GetDetectionPriority(Vector3 startPosition, Vector3 samplePosition)
        {
            bool anyHigh = false;
            bool anyLow = false;

            if (ScrollHorizontal)
            {
                int dir = (int)Mathf.Sign((startPosition - samplePosition).x);
                if (dir != 0 && HasContentInDirection(0, dir, out _))
                {
                    anyHigh = true;
                }
                else if (dir != 0)
                {
                    anyLow = true;
                }
            }

            if (ScrollVertical)
            {
                int dir = (int)Mathf.Sign((startPosition - samplePosition).y);
                if (dir != 0 && HasContentInDirection(1, dir, out _))
                {
                    anyHigh = true;
                }
                else if (dir != 0)
                {
                    anyLow = true;
                }
            }

            if (anyHigh)
            {
                return GestureState.DetectedHighPri;
            }

            return anyLow ? GestureState.DetectedLowPri : GestureState.None;
        }

        private bool HasContentInDirection(int axisIndex, int direction, out float amount)
        {
            amount = 0;

            float extentPoint = ScrollViewport.CalculatedPadding.Offset[axisIndex] + 0.5f * direction * ScrollViewport.PaddedSize[axisIndex];
            float contentPoint = ScrollViewport.ContentCenter[axisIndex] + 0.5f * direction * GetScrollableContentSize(axisIndex);

            amount = direction * (contentPoint - extentPoint);

            float content = Mathf.Abs(contentPoint);
            float extent = Mathf.Abs(extentPoint);

            return !Math.ApproximatelyEqual(content, extent) && extent < content;
        }

        bool IGestureRecognizer.ObstructDrags => ObstructDrags;

        #endregion

        #region Navigation

        bool INavigationNode.Enabled => IsNavigable;
        bool INavigationNode.CaptureInput => OnSelect == SelectBehavior.FireEvents;
        bool INavigationNode.ScopeNavigation => OnSelect == SelectBehavior.ScopeNavigation;

        bool INavigationNode.UseTargetNotFoundFallback(Vector3 direction)
        {
            if (TryGetPrimaryNavAxis(direction, out int axis, out int axisDirection))
            {
                return !HasContentInDirection(axis, axisDirection, out _);
            }

            return true;
        }

        bool INavigationNode.TryGetNext(Vector3 direction, out IUIBlock toUIBlock)
        {
            toUIBlock = null;

            if (!IsNavigable)
            {
                return false;
            }

            return Navigation.TryGetNavigation(direction, out toUIBlock);
        }

        bool INavigationNode.TryHandleScopedMove(IUIBlock previousChild, IUIBlock nextChild, Vector3 direction)
        {
            if (!IsNavigable)
            {
                return false;
            }

            if (!TryGetPrimaryNavAxis(direction, out int axis, out int axisDirection))
            {
                return false;
            }

            if ((axis == 0 && !ScrollHorizontal) || (axis == 1 && !ScrollVertical))
            {
                return false;
            }

            if (previousChild == null)
            {
                if (HasContentInDirection(axis, axisDirection, out float amount))
                {
                    float scroll = Math.MinAbs(ScrollViewport.GetChildAtIndex(0).LayoutSize[axis] + ScrollViewport.CalculatedSpacing.Value, amount);
                    Scroll(axis == 0 ? new Vector2(scroll * -axisDirection, 0) : new Vector2(0, scroll * -axisDirection));
                    return true;
                }

                return false;
            }

            if (nextChild == null)
            {
                if (HasContentInDirection(axis, axisDirection, out float amount))
                {
                    float scroll = Math.MinAbs(previousChild.LayoutSize[axis] + ScrollViewport.CalculatedSpacing.Value, amount);
                    Scroll(axis == 0 ? new Vector2(scroll * axisDirection, 0) : new Vector2(0, scroll * axisDirection));
                    return true;
                }

                return false;
            }

            return false;
        }

        void INavigationNode.EnsureInView(IUIBlock descendant)
        {
            if (descendant == null || content == null)
            {
                return;
            }

            for (int axis = 0; axis < 2; axis++)
            {
                if ((axis == 0 && !ScrollHorizontal) || (axis == 1 && !ScrollVertical))
                {
                    continue;
                }

                Vector3 worldPosition = LayoutDataStore.Instance.GetLocalToWorldMatrix(descendant).c3.xyz;
                Vector3 localPosition = ScrollViewport.transform.InverseTransformPoint(worldPosition);

                float marginOffset = descendant.CalculatedMargin.Offset[axis];
                float spacingOffset = marginOffset + ScrollViewport.CalculatedPadding.Offset[axis];
                float position = localPosition[axis];
                float size = descendant.CalculatedSize[axis].Value;

                int align = axis == 0 ? (int)Content.Layout.Alignment.X : (int)Content.Layout.Alignment.Y;

                float layoutOffset = LayoutUtils.LocalPositionToLayoutOffset(position, size, ScrollViewport.PaddedSize[axis], spacingOffset, align);

                float scroll = LayoutUtils.GetMinDistanceToAncestorEdge(size, layoutOffset, marginOffset, ScrollViewport, axis, align);
                int dir = -(int)Mathf.Sign(scroll);

                if (!Math.ApproximatelyZero(scroll) && HasContentInDirection(axis, dir, out _))
                {
                    Vector2 d = axis == 0 ? new Vector2(scroll, 0) : new Vector2(0, scroll);
                    Scroll(d);
                }
            }
        }

        private bool TryGetPrimaryNavAxis(Vector3 direction, out int axisIndex, out int axisDirection)
        {
            axisIndex = -1;
            axisDirection = 0;

            float ax = ScrollHorizontal ? Mathf.Abs(direction.x) : 0f;
            float ay = ScrollVertical ? Mathf.Abs(direction.y) : 0f;

            if (ax <= 0f && ay <= 0f)
            {
                return false;
            }

            if (ax >= ay)
            {
                axisIndex = 0;
                axisDirection = (int)Mathf.Sign(direction.x);
            }
            else
            {
                axisIndex = 1;
                axisDirection = (int)Mathf.Sign(direction.y);
            }

            return axisDirection != 0;
        }

        #endregion
    }
}
