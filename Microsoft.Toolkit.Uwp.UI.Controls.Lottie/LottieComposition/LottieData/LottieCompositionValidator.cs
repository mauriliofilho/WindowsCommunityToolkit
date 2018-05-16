﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace LottieData
{
    /// <summary>
    /// Validates a <see cref="LottieComposition"/> against various rules.
    /// </summary>
#if !WINDOWS_UWP
    public
#endif
    sealed class LottieCompositionValidator
    {
        readonly LottieComposition _lottieComposition;
        readonly List<string> _issues = new List<string>();

        LottieCompositionValidator(LottieComposition lottieComposition)
        {
            _lottieComposition = lottieComposition;
        }

        /// <summary>
        /// Validates the given <see cref="LottieComposition"/> against all of the validation rules.
        /// Returns a list of validation issues, or an empty list if no issues were found.
        /// </summary>
        public static string[] Validate(LottieComposition lottieComposition)
        {
            if (lottieComposition == null)
            {
                throw new ArgumentNullException(nameof(lottieComposition));
            }

            var validator = new LottieCompositionValidator(lottieComposition);

            validator.ValidateLayerInPointBeforeOutPoint();
            validator.ValidateParentPointsToValidLayer();
            validator.ValidateNoParentCycles();
            return validator._issues.ToArray();
        }

        /// <summary>
        /// Validates that the in-point of each layer is before its out-point.
        /// </summary>
        void ValidateLayerInPointBeforeOutPoint()
        {
            ValidateLayerInPointBeforeOutPoint(_lottieComposition.Layers);
            foreach (var layersAsset in _lottieComposition.Assets.OfType<LayerCollectionAsset>())
            {
                ValidateLayerInPointBeforeOutPoint(layersAsset.Layers);
            }
        }

        /// <summary>
        /// Validates that the in-point of each layer is before its out-point.
        /// </summary>
        void ValidateLayerInPointBeforeOutPoint(LayerCollection layers)
        {
            foreach (var layer in layers.GetLayersBottomToTop())
            {
                if (layer.InPoint >= layer.OutPoint)
                {
                    _issues.Add($"Layer {layer.Name} has in-point after out-point.");
                }
            }
        }

        /// <summary>
        /// Validates that there are no cycles caused by Parent references.
        /// </summary>
        void ValidateNoParentCycles()
        {
            ValidateNoParentCycles(_lottieComposition.Layers);
            foreach (var layersAsset in _lottieComposition.Assets.OfType<LayerCollectionAsset>())
            {
                ValidateNoParentCycles(layersAsset.Layers);
            }
        }

        /// <summary>
        /// Validates that there are no cycles caused by Parent references.
        /// </summary>
        void ValidateNoParentCycles(LayerCollection layers)
        {
            // Holds the layers that are known to not be in a cycle.
            var notInCycles = new HashSet<Layer>();
            // Holds the layers that have parents and have not yet been proven to
            // not be in a cycle.
            var maybeInCycles = new HashSet<Layer>();

            // Divide each layer into either notIn a cycle, or maybeIn a cycle.
            foreach (var layer in layers.GetLayersBottomToTop())
            {
                if (layer.Parent == null)
                {
                    // A layer with no Parent is definitely notIn a cycle.
                    notInCycles.Add(layer);
                }
                else if (notInCycles.Contains(layers.GetLayerById(layer.Parent)))
                {
                    // The layer has a parent that is not in a cycle, so the layer is
                    // not in a cycle.
                    notInCycles.Add(layer);
                }
                else
                {
                    // The layer may be in a cycle.
                    maybeInCycles.Add(layer);
                }
            }

            // Keep removing maybeIns that are parented by notIns until there are no more that
            // can be removed.
            do
            {
                foreach (var layer in maybeInCycles)
                {
                    if (notInCycles.Contains(layers.GetLayerById(layer.Parent)))
                    {
                        // The layer has a parent that is not in a cycle, so the layer
                        // is not in a cycle.
                        notInCycles.Add(layer);
                    }
                }
                // Remove the maybeIns that have now been discovered to be notIns.
                // If at least one layer was discovered to be notIn, keep going.
            } while (maybeInCycles.RemoveWhere(layer => notInCycles.Contains(layer)) != 0);

            // No more notIns were discovered. All the maybeIns are definitely in cycles.
            foreach (var layer in maybeInCycles)
            {
                _issues.Add($"Layer with Parent {layer.Parent} is in a cycle.");
            }
        }

        /// <summary>
        /// Validates that all the Parent references are either null or refer to a layer in the same collection.
        /// </summary>
        void ValidateParentPointsToValidLayer()
        {
            ValidateParentPointsToValidLayer(_lottieComposition.Layers);
            foreach (var layersAsset in _lottieComposition.Assets.OfType<LayerCollectionAsset>())
            {
                ValidateNoParentCycles(layersAsset.Layers);
            }
        }

        /// <summary>
        /// Validates that all the Parent references are either null or refer to a layer in the same collection.
        /// </summary>
        void ValidateParentPointsToValidLayer(LayerCollection layers)
        {
            foreach (var layer in layers.GetLayersBottomToTop())
            {
                if (layer.Parent.HasValue && layers.GetLayerById(layer.Parent) == null)
                {
                    _issues.Add($"Layer Parent {layer.Parent} is invalid.");
                }
            }
        }
    }

}