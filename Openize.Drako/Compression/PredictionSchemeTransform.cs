﻿using System;
using System.Collections.Generic;
using System.Text;
using Openize.Draco.Decoder;
using Openize.Draco.Encoder;
using Openize.Draco.Utils;

namespace Openize.Draco.Compression
{

    /// <summary>
    /// PredictionSchemeTransform is used to transform predicted values into
    /// correction values and vice versa.
    /// TDataType is the data type of predicted valuesPredictionSchemeTransform.
    /// CorrTypeT is the data type used for storing corrected values. It allows
    /// transforms to store corrections into a different type or format compared to
    /// the predicted data.
    /// </summary>
    class PredictionSchemeTransform
    {

        protected int numComponents;


        public virtual PredictionSchemeTransformType Type
        {
            get { return PredictionSchemeTransformType.Delta; }
        }

        /// <summary>
        /// Performs any custom initialization of the trasnform for the encoder.
        /// |size| = total number of values in |origData| (i.e., number of entries *
        /// number of components).
        /// </summary>
        public virtual void InitializeEncoding(IntArray origData, int numComponents)
        {
            this.numComponents = numComponents;
        }

        public virtual void InitializeDecoding(int numComponents)
        {
            this.numComponents = numComponents;
        }

        /// <summary>
        /// Computes the corrections based on the input original values and the
        /// predicted values. The correction is always computed for all components
        /// of the input element. |valId| is the id of the input value
        /// (i.e., elementId * numComponents). The default implementation is equal to
        /// std::minus.
        /// </summary>
        public virtual void ComputeCorrection(IntArray originalVals, int originalOffset,
            IntArray predictedVals, int predictedOffset,
            IntArray outCorrVals, int outOffset, int valId)
        {
            outOffset += valId;
            for (int i = 0; i < numComponents; ++i)
            {
                outCorrVals[outOffset + i] = originalVals[originalOffset + i] - predictedVals[predictedOffset + i];
            }
        }

        public void ComputeCorrection(IntArray originalVals,
            IntArray predictedVals,
            IntArray outCorrVals, int valId)
        {
            ComputeCorrection(originalVals, 0, predictedVals, 0, outCorrVals, 0, valId);
        }

        /// <summary>
        /// Computes the original value from the input predicted value and the decoded
        /// corrections. The default implementation is equal to std:plus.
        /// </summary>
        public virtual void ComputeOriginalValue(IntArray predictedVals,
            int predictedOffset,
            IntArray corrVals,
            int corrOffset,
            IntArray outOriginalVals,
            int outOffset)
        {
            for (int i = 0; i < numComponents; ++i)
            {
                outOriginalVals[i + outOffset] = predictedVals[i + predictedOffset] + corrVals[i + corrOffset];
            }
        }
        public void ComputeOriginalValue(IntArray predictedVals,
            IntArray corrVals,
            IntArray outOriginalVals)
        {
            ComputeOriginalValue(predictedVals, 0, corrVals, 0, outOriginalVals, 0);
        }

        /// <summary>
        /// Encode any transform specific data.
        /// </summary>
        public virtual bool EncodeTransformData(EncoderBuffer buffer)
        {
            return true;
        }

        /// <summary>
        /// Decodes any transform specific data. Called before Initialize() method.
        /// </summary>
        public virtual bool DecodeTransformData(DecoderBuffer buffer)
        {
            return true;
        }

        /// <summary>
        /// Should return true if all corrected values are guaranteed to be positive.
        /// </summary>
        public virtual bool AreCorrectionsPositive()
        {
            return false;
        }

    }
}
