﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing.Imaging;
using System.Drawing;

namespace dg.Utilities.Imaging.Processing.Filters
{
    public class Blur : ConvolutionMatrix
    {
        public new FilterError ProcessImage(
            DirectAccessBitmap bmp,
            params object[] args)
        {
            Matrix3x3 kernel = new Matrix3x3(
                1, 1, 1,
                1, 1, 1,
                1, 1, 1,
                1, 0, true);
            FilterColorChannel channels = FilterColorChannel.None;

            foreach (object arg in args)
            {
                if (arg is FilterColorChannel)
                {
                    channels |= (FilterColorChannel)arg;
                }
            }

            return base.ProcessImage(bmp, kernel, channels);
        }
    }
}
