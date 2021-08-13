// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using Microsoft.Graphics.Canvas.Geometry;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;

namespace Microsoft.Toolkit.Uwp.UI.Media
{
    public class AttachedCardShadow : AttachedShadowBase
    {
        private const float MaxBlurRadius = 72;
        private static readonly TypedResourceKey<CompositionGeometricClip> ClipResourceKey = "Clip";
        private static readonly bool SupportsCompositionVisualSurface;

        private static readonly TypedResourceKey<CompositionPathGeometry> PathGeometryResourceKey = "PathGeometry";
        private static readonly TypedResourceKey<CompositionRoundedRectangleGeometry> RoundedRectangleGeometryResourceKey = "RoundedGeometry";
        private static readonly TypedResourceKey<CompositionSpriteShape> ShapeResourceKey = "Shape";
        private static readonly TypedResourceKey<ShapeVisual> ShapeVisualResourceKey = "ShapeVisual";
        private static readonly TypedResourceKey<CompositionSurfaceBrush> SurfaceBrushResourceKey = "SurfaceBrush";
        private static readonly TypedResourceKey<CompositionVisualSurface> VisualSurfaceResourceKey = "VisualSurface";

        /// <summary>
        /// The <see cref="DependencyProperty"/> for <see cref="CornerRadius"/>
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(double),
                typeof(AttachedCardShadow),
                new PropertyMetadata(4d, OnDependencyPropertyChanged)); // Default WinUI ControlCornerRadius is 4

        static AttachedCardShadow()
        {
            SupportsCompositionVisualSurface = ApiInformation.IsTypePresent(typeof(CompositionVisualSurface).FullName); // Note: This is 1903 (18362) min
        }

        /// <summary>
        /// Gets or sets the roundness of the shadow's corners.
        /// </summary>
        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <inheritdoc/>
        public override bool IsSupported => SupportsCompositionVisualSurface;

        /// <inheritdoc/>
        protected override bool SupportsOnSizeChangedEvent => true;

        /// <inheritdoc/>
        protected override void OnElementContextUninitialized(AttachedShadowElementContext context)
        {
            context.ClearAndDisposeResources();
            base.OnElementContextUninitialized(context);
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AttachedShadowElementContext context, DependencyProperty property, object oldValue, object newValue)
        {
            if (property == CornerRadiusProperty)
            {
                var geometry = context.GetResource(RoundedRectangleGeometryResourceKey);
                if (geometry != null)
                {
                    geometry.CornerRadius = new Vector2((float)(double)newValue);
                }

                UpdateShadowClip(context);
            }
            else
            {
                base.OnPropertyChanged(context, property, oldValue, newValue);
            }
        }

        /// <inheritdoc/>
        protected override CompositionBrush GetShadowMask(AttachedShadowElementContext context)
        {
            if (!SupportsCompositionVisualSurface)
            {
                return null;
            }

            // Create rounded rectangle geometry and add it to a shape
            var geometry = context.GetResource(RoundedRectangleGeometryResourceKey) ?? context.AddResource(
                RoundedRectangleGeometryResourceKey,
                context.Compositor.CreateRoundedRectangleGeometry());
            geometry.CornerRadius = new Vector2((float)CornerRadius);

            var shape = context.GetResource(ShapeResourceKey) ?? context.AddResource(ShapeResourceKey, context.Compositor.CreateSpriteShape(geometry));
            shape.FillBrush = context.Compositor.CreateColorBrush(Colors.Black);

            // Create a ShapeVisual so that our geometry can be rendered to a visual
            var shapeVisual = context.GetResource(ShapeVisualResourceKey) ??
                              context.AddResource(ShapeVisualResourceKey, context.Compositor.CreateShapeVisual());
            shapeVisual.Shapes.Add(shape);

            // Create a CompositionVisualSurface, which renders our ShapeVisual to a texture
            var visualSurface = context.GetResource(VisualSurfaceResourceKey) ??
                                context.AddResource(VisualSurfaceResourceKey, context.Compositor.CreateVisualSurface());
            visualSurface.SourceVisual = shapeVisual;

            // Create a CompositionSurfaceBrush to render our CompositionVisualSurface to a brush.
            // Now we have a rounded rectangle brush that can be used on as the mask for our shadow.
            var surfaceBrush = context.GetResource(SurfaceBrushResourceKey) ?? context.AddResource(
                SurfaceBrushResourceKey,
                context.Compositor.CreateSurfaceBrush(visualSurface));

            geometry.Size = visualSurface.SourceSize = shapeVisual.Size = context.Element.RenderSize.ToVector2();

            return surfaceBrush;
        }

        /// <inheritdoc/>
        protected override CompositionClip GetShadowClip(AttachedShadowElementContext context)
        {
            var pathGeom = context.GetResource(PathGeometryResourceKey) ??
                           context.AddResource(PathGeometryResourceKey, context.Compositor.CreatePathGeometry());
            var clip = context.GetResource(ClipResourceKey) ?? context.AddResource(ClipResourceKey, context.Compositor.CreateGeometricClip(pathGeom));

            // Create rounded rectangle geometry at a larger size that compensates for the size of the stroke,
            // as we want the inside edge of the stroke to match the edges of the element.
            // Additionally, the inside edge of the stroke will have a smaller radius than the radius we specified.
            // Using "(StrokeThickness / 2) + Radius" as our rectangle's radius will give us an inside stroke radius that matches the radius we want.
            var canvasRectangle = CanvasGeometry.CreateRoundedRectangle(
                null,
                -MaxBlurRadius / 2,
                -MaxBlurRadius / 2,
                (float)context.Element.ActualWidth + MaxBlurRadius,
                (float)context.Element.ActualHeight + MaxBlurRadius,
                (MaxBlurRadius / 2) + (float)CornerRadius,
                (MaxBlurRadius / 2) + (float)CornerRadius);

            var canvasStroke = canvasRectangle.Stroke(MaxBlurRadius);

            pathGeom.Path = new CompositionPath(canvasStroke);

            return clip;
        }

        /// <inheritdoc/>
        protected override void OnSizeChanged(AttachedShadowElementContext context, Size newSize, Size previousSize)
        {
            var sizeAsVec2 = newSize.ToVector2();

            var geometry = context.GetResource(RoundedRectangleGeometryResourceKey);
            if (geometry != null)
            {
                geometry.Size = sizeAsVec2;
            }

            var visualSurface = context.GetResource(VisualSurfaceResourceKey);
            if (geometry != null)
            {
                visualSurface.SourceSize = sizeAsVec2;
            }

            var shapeVisual = context.GetResource(ShapeVisualResourceKey);
            if (geometry != null)
            {
                shapeVisual.Size = sizeAsVec2;
            }

            UpdateShadowClip(context);

            base.OnSizeChanged(context, newSize, previousSize);
        }
    }
}