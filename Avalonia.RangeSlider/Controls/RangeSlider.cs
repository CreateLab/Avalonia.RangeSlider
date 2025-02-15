﻿using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Mixins;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.RangeSlider.Controls.Primitives;
using Avalonia.Utilities;
using RangeBase = Avalonia.RangeSlider.Controls.Primitives.RangeBase;

namespace Avalonia.RangeSlider.Controls
{
    /// <summary>
    /// Enum which describes how to position the flyout in a <see cref="RangeSlider"/>.
    /// </summary>
    public enum ThumbFlyoutPlacement
    {
        /// <summary>
        /// No flyout will appear.
        /// </summary>
        None,

        /// <summary>
        /// Flyout will appear above the track for a horizontal <see cref="RangeSlider"/>, or to the left of the track for a vertical <see cref="RangeSlider"/>.
        /// </summary>
        TopLeft,

        /// <summary>
        /// Flyout will appear below the track for a horizontal <see cref="RangeSlider"/>, or to the right of the track for a vertical <see cref="RangeSlider"/>.
        /// </summary>
        BottomRight,
    }

    /// <summary>
    /// A control that lets the user select from a range of values by moving a Thumb control along a Track.
    /// </summary>
    [PseudoClasses(":vertical", ":horizontal", ":pressed")]
    public class RangeSlider : RangeBase
    {
        private enum TrackThumb
        {
            None,
            Upper,
            InnerUpper,
            OuterUpper,
            Lower,
            InnerLower,
            OuterLower,
            Both,
            Overlapped
        };

        /// <summary>
        /// Defines the <see cref="Orientation"/> property.
        /// </summary>
        public static readonly StyledProperty<Orientation> OrientationProperty =
            ScrollBar.OrientationProperty.AddOwner<RangeSlider>();

        /// <summary>
        /// Defines the <see cref="IsDirectionReversed"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsDirectionReversedProperty =
            RangeTrack.IsDirectionReversedProperty.AddOwner<RangeSlider>();

        /// <summary>
        /// Defines the <see cref="IsThumbOverlapProperty"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsThumbOverlapProperty =
            RangeTrack.IsThumbOverlapProperty.AddOwner<RangeSlider>();

        /// <summary>
        /// Defines the <see cref="FlyoutPlacement"/> property.
        /// </summary>
        public static readonly StyledProperty<ThumbFlyoutPlacement> ThumbFlyoutPlacementProperty =
            AvaloniaProperty.Register<TickBar, ThumbFlyoutPlacement>(nameof(ThumbFlyoutPlacement), ThumbFlyoutPlacement.None);

        /// <summary>
        /// Defines the <see cref="IsSnapToTickEnabled"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsSnapToTickEnabledProperty =
            AvaloniaProperty.Register<RangeSlider, bool>(nameof(IsSnapToTickEnabled), false);

        /// <summary>
        /// Defines the <see cref="MoveWholeRange"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> MoveWholeRangeProperty =
            AvaloniaProperty.Register<RangeSlider, bool>(nameof(MoveWholeRange), false);

        /// <summary>
        /// Defines the <see cref="TickFrequency"/> property.
        /// </summary>
        public static readonly StyledProperty<double> TickFrequencyProperty =
            AvaloniaProperty.Register<RangeSlider, double>(nameof(TickFrequency), 0.0);

        /// <summary>
        /// Defines the <see cref="TickPlacement"/> property.
        /// </summary>
        public static readonly StyledProperty<TickPlacement> TickPlacementProperty =
            AvaloniaProperty.Register<TickBar, TickPlacement>(nameof(TickPlacement), 0d);

        /// <summary>
        /// Defines the <see cref="TicksProperty"/> property.
        /// </summary>
        public static readonly StyledProperty<AvaloniaList<double>> TicksProperty =
            TickBar.TicksProperty.AddOwner<RangeSlider>();

        // Slider required parts
        private double _previousValue = 0.0;
        private bool _isDragging = false;
        private RangeTrack _track = null!;
        private Thumb _lowerThumb = null!;
        private Thumb _upperThumb = null!;
        private TrackThumb _currentTrackThumb = TrackThumb.None;

        private const double Tolerance = 0.0001;

        /// <summary>
        /// Initializes static members of the <see cref="RangeSlider"/> class. 
        /// </summary>
        static RangeSlider()
        {
            PressedMixin.Attach<RangeSlider>();
            FocusableProperty.OverrideDefaultValue<RangeSlider>(true);
            OrientationProperty.OverrideDefaultValue(typeof(RangeSlider), Orientation.Horizontal);

            LowerSelectedValueProperty.OverrideMetadata<RangeSlider>(new DirectPropertyMetadata<double>(enableDataValidation: true));
            UpperSelectedValueProperty.OverrideMetadata<RangeSlider>(new DirectPropertyMetadata<double>(enableDataValidation: true));

            ThumbFlyoutPlacementProperty.Changed.AddClassHandler<RangeSlider>((x, e) => x.ThumbFlyoutPlacementChanged(e));
        }

        /// <summary>
        /// Instantiates a new instance of the <see cref="RangeSlider"/> class. 
        /// </summary>
        public RangeSlider()
        {
            UpdatePseudoClasses(Orientation);
        }

        /// <summary>
        /// Defines the ticks to be drawn on the tick bar.
        /// </summary>
        public AvaloniaList<double> Ticks
        {
            get => GetValue(TicksProperty);
            set => SetValue(TicksProperty, value);
        }

        /// <summary>
        /// Gets or sets the orientation of a <see cref="RangeSlider"/>.
        /// </summary>
        public Orientation Orientation
        {
            get { return GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value allowing to move the whole selected range.
        /// </summary>
        public bool MoveWholeRange
        {
            get { return GetValue(MoveWholeRangeProperty); }
            set { SetValue(MoveWholeRangeProperty, value); }
        }

        /// <summary>
        /// Gets or sets the direction of increasing value.
        /// </summary>
        /// <value>
        /// true if the direction of increasing value is to the left for a horizontal slider or
        /// down for a vertical slider; otherwise, false. The default is false.
        /// </value>
        public bool IsDirectionReversed
        {
            get { return GetValue(IsDirectionReversedProperty); }
            set { SetValue(IsDirectionReversedProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the current value will be displayed above <see cref="Thumb"/>.
        /// </summary>
        public ThumbFlyoutPlacement ThumbFlyoutPlacement
        {
            get { return GetValue(ThumbFlyoutPlacementProperty); }
            set { SetValue(ThumbFlyoutPlacementProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the <see cref="RangeSlider"/> automatically moves the <see cref="Thumb"/> to the closest tick mark.
        /// </summary>
        public bool IsSnapToTickEnabled
        {
            get { return GetValue(IsSnapToTickEnabledProperty); }
            set { SetValue(IsSnapToTickEnabledProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether the <see cref="Thumb"/> can overlap.
        /// </summary>
        public bool IsThumbOverlap
        {
            get { return GetValue(IsThumbOverlapProperty); }
            set { SetValue(IsThumbOverlapProperty, value); }
        }

        /// <summary>
        /// Gets or sets the interval between tick marks.
        /// </summary>
        public double TickFrequency
        {
            get { return GetValue(TickFrequencyProperty); }
            set { SetValue(TickFrequencyProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value that indicates where to draw 
        /// tick marks in relation to the track.
        /// </summary>
        public TickPlacement TickPlacement
        {
            get { return GetValue(TickPlacementProperty); }
            set { SetValue(TickPlacementProperty, value); }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _track = e.NameScope.Find<RangeTrack>("PART_Track");
            _lowerThumb = e.NameScope.Find<Thumb>("PART_LowerThumb");
            _upperThumb = e.NameScope.Find<Thumb>("PART_UpperThumb");

            ApplyThumbFlyoutPlacement(ThumbFlyoutPlacement);

            AddHandler(PointerPressedEvent, TrackPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerMovedEvent, TrackMoved, RoutingStrategies.Tunnel);
            AddHandler(PointerReleasedEvent, TrackReleased, RoutingStrategies.Tunnel);

            _lowerThumb.AddHandler(PointerMovedEvent, PointerOverThumb, RoutingStrategies.Tunnel);
            _upperThumb.AddHandler(PointerMovedEvent, PointerOverThumb, RoutingStrategies.Tunnel);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled || e.KeyModifiers != KeyModifiers.None) return;

            var handled = true;

            switch (e.Key)
            {
                case Key.Down:
                case Key.Left:
                    MoveToNextTick(IsDirectionReversed ? SmallChange : -SmallChange);
                    break;

                case Key.Up:
                case Key.Right:
                    MoveToNextTick(IsDirectionReversed ? -SmallChange : SmallChange);
                    break;

                case Key.PageUp:
                    MoveToNextTick(IsDirectionReversed ? -LargeChange : LargeChange);
                    break;

                case Key.PageDown:
                    MoveToNextTick(IsDirectionReversed ? LargeChange : -LargeChange);
                    break;

                case Key.Home:
                    LowerSelectedValue = Minimum;
                    break;

                case Key.End:
                    UpperSelectedValue = Maximum;
                    break;

                default:
                    handled = false;
                    break;
            }

            e.Handled = handled;
        }

        private void MoveToNextTick(double direction)
        {
            if (direction == 0.0) return;

            var value = LowerSelectedValue;

            // Find the next value by snapping
            var next = SnapToTick(Math.Max(Minimum, Math.Min(Maximum, value + direction)));

            var greaterThan = direction > 0; //search for the next tick greater than value?

            // If the snapping brought us back to value, find the next tick point
            if (Math.Abs(next - value) < Tolerance
                && !(greaterThan && Math.Abs(value - Maximum) < Tolerance) // Stop if searching up if already at Max
                && !(!greaterThan && Math.Abs(value - Minimum) < Tolerance)) // Stop if searching down if already at Min
            {
                var ticks = Ticks;

                // If ticks collection is available, use it.
                // Note that ticks may be unsorted.
                if (ticks != null && ticks.Count > 0)
                {
                    foreach (var tick in ticks)
                    {
                        // Find the smallest tick greater than value or the largest tick less than value
                        if (greaterThan && MathUtilities.GreaterThan(tick, value) &&
                            (MathUtilities.LessThan(tick, next) || Math.Abs(next - value) < Tolerance)
                            || !greaterThan && MathUtilities.LessThan(tick, value) &&
                            (MathUtilities.GreaterThan(tick, next) || Math.Abs(next - value) < Tolerance))
                        {
                            next = tick;
                        }
                    }
                }
                else if (MathUtilities.GreaterThan(TickFrequency, 0.0))
                {
                    // Find the current tick we are at
                    var tickNumber = Math.Round((value - Minimum) / TickFrequency);

                    if (greaterThan)
                        tickNumber += 1.0;
                    else
                        tickNumber -= 1.0;

                    next = Minimum + tickNumber * TickFrequency;
                }
            }

            // Update if we've found a better value
            if (Math.Abs(next - value) > Tolerance)
            {
                LowerSelectedValue = next;
            }
        }

        private void PointerOverThumb(object? sender, PointerEventArgs e)
        {
            if (ThumbFlyoutPlacement == ThumbFlyoutPlacement.None)
                return;

            if (sender is Thumb thumb)
                FlyoutBase.ShowAttachedFlyout(thumb);
        }

        private void TrackPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;

                var pointerCoord = e.GetCurrentPoint(_track).Position;
                _previousValue = GetValueByPointOnTrack(pointerCoord);
                _currentTrackThumb = GetNearestTrackThumb(pointerCoord);

                if (!IsPressedOnTrackBetweenThumbs() && MoveWholeRange)
                    MoveToPoint(pointerCoord, _currentTrackThumb);
                else if (!MoveWholeRange && _currentTrackThumb != TrackThumb.Overlapped)
                    MoveToPoint(pointerCoord, _currentTrackThumb);
            }
        }

        private void TrackReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
            _currentTrackThumb = TrackThumb.None;
        }

        private void TrackMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                var pointerCoord = e.GetCurrentPoint(_track).Position;

                if (_currentTrackThumb == TrackThumb.Overlapped)
                    SelectThumbBasedOnPointerDirection(pointerCoord);

                if (!IsPressedOnTrackBetweenThumbs() && MoveWholeRange)
                    MoveToPoint(pointerCoord, _currentTrackThumb);
                if (IsPressedOnTrackBetweenThumbs() && MoveWholeRange)
                    MoveToPoint(pointerCoord, TrackThumb.Both);
                else if (!MoveWholeRange)
                    MoveToPoint(pointerCoord, _currentTrackThumb);
            }
        }

        private void MoveToPoint(Point pointerCoord, TrackThumb trackThumb)
        {
            var value = GetValueByPointOnTrack(pointerCoord);

            switch (trackThumb)
            {
                case TrackThumb.Upper:
                case TrackThumb.InnerUpper:
                case TrackThumb.OuterUpper:
                    UpperSelectedValue = SnapToTick(value);
                    break;
                case TrackThumb.Lower:
                case TrackThumb.InnerLower:
                case TrackThumb.OuterLower:
                    LowerSelectedValue = SnapToTick(value);
                    break;
                case TrackThumb.Both:
                    var delta = value - _previousValue;
                    
                    if ((Math.Abs(LowerSelectedValue - Minimum) <= Tolerance && delta <= 0d)
                        || (Math.Abs(UpperSelectedValue - Maximum) <= Tolerance && delta >= 0d))
                        return;

                    if (!IsSnapToTickEnabled)
                    {
                        _previousValue = value;
                        LowerSelectedValue += delta;
                        UpperSelectedValue += delta;
                    }
                    else
                    {
                        var closestTick = SnapToTick(Math.Abs(delta) / 2d);
                        if (closestTick > 0d)
                        {
                            _previousValue = value;
                            LowerSelectedValue += closestTick * Math.Sign(delta);
                            UpperSelectedValue += closestTick * Math.Sign(delta);
                        }
                    }
                    break;
            }
        }

        private double GetValueByPointOnTrack(Point pointerCoord)
        {
            var orient = Orientation == Orientation.Horizontal;
            var trackLength = orient ? _track.Bounds.Width : _track.Bounds.Height;
            var pointNum = orient ? pointerCoord.X : pointerCoord.Y;
            var thumbLength = orient ? _track.LowerThumb.Width : _track.LowerThumb.Height;

            // Just add epsilon to avoid NaN in case 0/0
            trackLength += double.Epsilon;

            if (IsThumbOverlap)
                thumbLength /= 2.0;

            if (pointNum <= thumbLength)
                return orient ? Minimum : Maximum;
            if (pointNum > trackLength - thumbLength)
                return orient ? Maximum : Minimum;

            trackLength -= 2.0 * thumbLength;
            pointNum -= thumbLength;

            var logicalPos = MathUtilities.Clamp(pointNum / trackLength, 0.0d, 1.0d);
            var invert = orient
                ? IsDirectionReversed ? 1 : 0
                : IsDirectionReversed ? 0 : 1;
            var calcVal = Math.Abs(invert - logicalPos);
            var range = Maximum - Minimum;
            var finalValue = calcVal * range + Minimum;

            return finalValue;
        }

        private bool IsPressedOnTrackBetweenThumbs()
        {
            return _currentTrackThumb == TrackThumb.InnerLower || _currentTrackThumb == TrackThumb.InnerUpper;
        }

        private void SelectThumbBasedOnPointerDirection(Point pointerCoord)
        {
            var value = GetValueByPointOnTrack(pointerCoord);
            var delta = _previousValue - value;

            if (delta >= 0d && delta < Tolerance)
                return;

            if (delta > 0d)
                _currentTrackThumb = TrackThumb.Lower;
            else
                _currentTrackThumb = TrackThumb.Upper;
        }

        private TrackThumb GetNearestTrackThumb(Point pointerCoord)
        {
            var orient = Orientation == Orientation.Horizontal;

            var lowerThumbPos = orient ? _track.LowerThumb.Bounds.Position.X : _track.LowerThumb.Bounds.Position.Y;
            var upperThumbPos = orient ? _track.UpperThumb.Bounds.Position.X : _track.UpperThumb.Bounds.Position.Y;
            var thumbWidth = orient ? _track.LowerThumb.Bounds.Width : _track.LowerThumb.Bounds.Height;
            var thumbHalfWidth = thumbWidth / 2d;

            var pointerPos = orient ? pointerCoord.X : pointerCoord.Y;

            if (IsThumbOverlap)
            {
                var isThumbsOverlapped = Math.Abs(lowerThumbPos - upperThumbPos) < Tolerance;

                if (isThumbsOverlapped)
                    return TrackThumb.Overlapped;
            }

            if (Math.Abs(lowerThumbPos + thumbHalfWidth - pointerPos) <= thumbHalfWidth)
                return TrackThumb.Lower;
            else if (Math.Abs(upperThumbPos + thumbHalfWidth - pointerPos) <= thumbHalfWidth)
                return TrackThumb.Upper;

            if (Math.Abs(lowerThumbPos - pointerPos) < Math.Abs(upperThumbPos - pointerPos))
            {
                if (pointerPos < lowerThumbPos)
                    return orient ? TrackThumb.OuterLower : TrackThumb.InnerLower;
                else
                    return orient ? TrackThumb.InnerLower : TrackThumb.OuterLower;
            }
            else
            {
                if (pointerPos < upperThumbPos)
                    return orient ? TrackThumb.InnerUpper : TrackThumb.OuterUpper;
                else
                    return orient ? TrackThumb.OuterUpper : TrackThumb.InnerUpper;
            }
        }

        protected override void UpdateDataValidation<T>(AvaloniaProperty<T> property, BindingValue<T> value)
        {
            if (property == LowerSelectedValueProperty || property == UpperSelectedValueProperty)
            {
                DataValidationErrors.SetError(this, value.Error);
            }
        }

        protected override void OnPropertyChanged<T>(AvaloniaPropertyChangedEventArgs<T> change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == OrientationProperty)
            {
                UpdatePseudoClasses(change.NewValue.GetValueOrDefault<Orientation>());
            }
        }

        /// <summary>
        /// Snap the input 'value' to the closest tick.
        /// </summary>
        /// <param name="value">Value that want to snap to closest Tick.</param>
        private double SnapToTick(double value)
        {
            if (IsSnapToTickEnabled)
            {
                var previous = Minimum;
                var next = Maximum;

                // This property is rarely set so let's try to avoid the GetValue
                var ticks = Ticks;

                // If ticks collection is available, use it.
                // Note that ticks may be unsorted.
                if (ticks != null && ticks.Count > 0)
                {
                    foreach (var tick in ticks)
                    {
                        if (MathUtilities.AreClose(tick, value))
                        {
                            return value;
                        }

                        if (MathUtilities.LessThan(tick, value) && MathUtilities.GreaterThan(tick, previous))
                        {
                            previous = tick;
                        }
                        else if (MathUtilities.GreaterThan(tick, value) && MathUtilities.LessThan(tick, next))
                        {
                            next = tick;
                        }
                    }
                }
                else if (MathUtilities.GreaterThan(TickFrequency, 0.0))
                {
                    previous = Minimum + Math.Round((value - Minimum) / TickFrequency) * TickFrequency;
                    next = Math.Min(Maximum, previous + TickFrequency);
                }

                // Choose the closest value between previous and next. If tie, snap to 'next'.
                value = MathUtilities.GreaterThanOrClose(value, (previous + next) * 0.5) ? next : previous;
            }

            return value;
        }

        private void ApplyThumbFlyoutPlacement(ThumbFlyoutPlacement placement)
        {
            if (placement == ThumbFlyoutPlacement.None)
                return;

            var placementMode = FlyoutPlacementMode.Auto;

            if (placement == ThumbFlyoutPlacement.TopLeft && Orientation == Orientation.Horizontal)
                placementMode = FlyoutPlacementMode.Top;
            else if (placement == ThumbFlyoutPlacement.TopLeft && Orientation == Orientation.Vertical)
                placementMode = FlyoutPlacementMode.Left;
            else if (placement == ThumbFlyoutPlacement.BottomRight && Orientation == Orientation.Horizontal)
                placementMode = FlyoutPlacementMode.Bottom;
            else if (placement == ThumbFlyoutPlacement.BottomRight && Orientation == Orientation.Vertical)
                placementMode = FlyoutPlacementMode.Right;

            var lowerFlyout = FlyoutBase.GetAttachedFlyout(_lowerThumb);
            if (lowerFlyout != null)
                lowerFlyout.Placement = placementMode;

            var upperFlyout = FlyoutBase.GetAttachedFlyout(_upperThumb);
            if (upperFlyout != null)
                upperFlyout.Placement = placementMode;
        }

        private void ThumbFlyoutPlacementChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lowerThumb == null || _upperThumb == null)
                return;

            if (e.NewValue is ThumbFlyoutPlacement placement)
            {
                ApplyThumbFlyoutPlacement(placement);
            }
        }

        private void UpdatePseudoClasses(Orientation o)
        {
            PseudoClasses.Set(":vertical", o == Orientation.Vertical);
            PseudoClasses.Set(":horizontal", o == Orientation.Horizontal);
        }
    }
}