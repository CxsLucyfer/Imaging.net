﻿#define UseDictionary

using System;
using System.Drawing;
using System.Collections.Generic;
using Imaging.net.Quantizers.ColorCaches;
using Imaging.net.Quantizers.ColorCaches.Octree;
using Imaging.net.Quantizers.Helpers;
using System.Linq;
using Imaging.net.Quantizers;

#if (UseDictionary)
    using System.Collections.Concurrent;
#endif

namespace Imaging.net.Quantizers.DistinctSelection
{
    /// <summary>
    /// Provided by SmartK8 on CodeProject. http://www.codeproject.com/Articles/66341/A-Simple-Yet-Quite-Powerful-Palette-Quantizer-in-C
    /// 
    /// This is my baby. Read more in the article on the Code Project:
    /// http://www.codeproject.com/KB/recipes/SimplePaletteQuantizer.aspx
    /// </summary>
    public class DistinctSelectionQuantizer : BaseColorCacheQuantizer
    {
        #region | Fields |

        private List<Color> palette;
        private Int32 foundColorCount;

#if (UseDictionary)
        private ConcurrentDictionary<Int32, DistinctColorInfo> colorMap;
#else
        private DistinctBucket rootBucket;
#endif

        #endregion

        #region | Methods |

        private static Boolean ProcessList(Int32 colorCount, List<DistinctColorInfo> list, ICollection<IEqualityComparer<DistinctColorInfo>> comparers, out List<DistinctColorInfo> outputList)
        {
            IEqualityComparer<DistinctColorInfo> bestComparer = null;
            Int32 maximalCount = 0;
            outputList = list;

            foreach (IEqualityComparer<DistinctColorInfo> comparer in comparers)
            {
                List<DistinctColorInfo> filteredList = list.
                    Distinct(comparer).
                    ToList();

                Int32 filteredListCount = filteredList.Count;

                if (filteredListCount > colorCount && filteredListCount > maximalCount)
                {
                    maximalCount = filteredListCount;
                    bestComparer = comparer;
                    outputList = filteredList;

                    if (maximalCount <= colorCount) break;
                }
            }

            comparers.Remove(bestComparer);
            return comparers.Count > 0 && maximalCount > colorCount;
        }

        #endregion

        #region << BaseColorCacheQuantizer >>

        /// <summary>
        /// See <see cref="IColorQuantizer.Prepare"/> for more details.
        /// </summary>
        protected override void OnPrepare(ImageBuffer image)
        {
            base.OnPrepare(image);

            OnFinish();
        }

        /// <summary>
        /// See <see cref="BaseColorCacheQuantizer.OnCreateDefaultCache"/> for more details.
        /// </summary>
        protected override IColorCache OnCreateDefaultCache()
        {
            // use OctreeColorCache best performance/quality
            return new OctreeColorCache();
        }

        /// <summary>
        /// See <see cref="BaseColorQuantizer.OnAddColor"/> for more details.
        /// </summary>
        protected override void OnAddColor(Color color, Int32 key, Int32 x, Int32 y)
        {
#if (UseDictionary)
            colorMap.AddOrUpdate(key,
                colorKey => new DistinctColorInfo(color),
                (colorKey, colorInfo) => colorInfo.IncreaseCount());
#else
            color = QuantizationHelper.ConvertAlpha(color);
            rootBucket.StoreColor(color);
#endif
        }

        /// <summary>
        /// See <see cref="BaseColorCacheQuantizer.OnGetPaletteToCache"/> for more details.
        /// </summary>
        protected override List<Color> OnGetPaletteToCache(Int32 colorCount)
        {
            // otherwise calculate one
            palette.Clear();

            // lucky seed :)
            FastRandom random = new FastRandom(13);

#if (UseDictionary)
            List<DistinctColorInfo> colorInfoList = new List<DistinctColorInfo>(colorMap.Values);
#else
            List<DistinctColorInfo> colorInfoList = rootBucket.GetValues().ToList();
#endif

            foundColorCount = colorInfoList.Count;

            if (foundColorCount >= colorCount)
            {
                // shuffles the colormap
                colorInfoList.Sort((DistinctColorInfo A, DistinctColorInfo B) => { return random.Next(foundColorCount); });

                // workaround for backgrounds, the most prevalent color
                DistinctColorInfo background = colorInfoList.MaxBy(info => info.Count);
                colorInfoList.Remove(background);
                colorCount--;

                ColorHueComparer hueComparer = new ColorHueComparer();
                ColorSaturationComparer saturationComparer = new ColorSaturationComparer();
                ColorBrightnessComparer brightnessComparer = new ColorBrightnessComparer();

                // generates catalogue
                List<IEqualityComparer<DistinctColorInfo>> comparers = new List<IEqualityComparer<DistinctColorInfo>> { hueComparer, saturationComparer, brightnessComparer };

                // take adequate number from each slot
                while (ProcessList(colorCount, colorInfoList, comparers, out colorInfoList)) { }

                Int32 listColorCount = colorInfoList.Count;

                if (listColorCount > 0)
                {
                    Int32 allowedTake = Math.Min(colorCount, listColorCount);
                    if (colorInfoList.Count > allowedTake)
                    {
                        colorInfoList.RemoveRange(allowedTake, colorInfoList.Count - allowedTake);
                    }
                }

                // adds background color first
                palette.Add(Color.FromArgb(background.Color));
            }

            // adds the selected colors to a final palette
            foreach (DistinctColorInfo colorInfo in colorInfoList)
            {
                palette.Add(Color.FromArgb(colorInfo.Color));
            }

            // returns our new palette
            return palette;
        }

        /// <summary>
        /// See <see cref="BaseColorQuantizer.GetColorCount"/> for more details.
        /// </summary>
        protected override Int32 OnGetColorCount()
        {
            return foundColorCount;
        }

        /// <summary>
        /// See <see cref="BaseColorQuantizer.OnFinish"/> for more details.
        /// </summary>
        protected override void OnFinish()
        {
            base.OnFinish();

            palette = new List<Color>();

#if (UseDictionary)
            colorMap = new ConcurrentDictionary<Int32, DistinctColorInfo>();
#else
            rootBucket = new DistinctBucket();
#endif
        }

        #endregion

        #region << IColorQuantizer >>

        /// <summary>
        /// See <see cref="IColorQuantizer.AllowParallel"/> for more details.
        /// </summary>
        public override Boolean AllowParallel
        {
            get { return true; }
        }

        #endregion

        #region | Helper classes (comparers) |

        /// <summary>
        /// Compares a hue components of a color info.
        /// </summary>
        private class ColorHueComparer : IEqualityComparer<DistinctColorInfo>
        {
            public Boolean Equals(DistinctColorInfo x, DistinctColorInfo y)
            {
                return x.Hue == y.Hue;
            }

            public Int32 GetHashCode(DistinctColorInfo colorInfo)
            {
                return colorInfo.Hue.GetHashCode();
            }
        }

        /// <summary>
        /// Compares a saturation components of a color info.
        /// </summary>
        private class ColorSaturationComparer : IEqualityComparer<DistinctColorInfo>
        {
            public Boolean Equals(DistinctColorInfo x, DistinctColorInfo y)
            {
                return x.Saturation == y.Saturation;
            }

            public Int32 GetHashCode(DistinctColorInfo colorInfo)
            {
                return colorInfo.Saturation.GetHashCode();
            }
        }

        /// <summary>
        /// Compares a brightness components of a color info.
        /// </summary>
        private class ColorBrightnessComparer : IEqualityComparer<DistinctColorInfo>
        {
            public Boolean Equals(DistinctColorInfo x, DistinctColorInfo y)
            {
                return x.Brightness == y.Brightness;
            }

            public Int32 GetHashCode(DistinctColorInfo colorInfo)
            {
                return colorInfo.Brightness.GetHashCode();
            }
        }

        #endregion
    }
}


