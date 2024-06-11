﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Openize.Draco.Compression;
using Openize.Draco.Utils;

namespace Openize.Draco.Decoder
{
    class PredictionSchemeDeltaDecoder : PredictionScheme
    {
        public override bool Initialized => true;
        public override PredictionSchemeMethod PredictionMethod => PredictionSchemeMethod.Difference;

        public PredictionSchemeDeltaDecoder(PointAttribute attribute, PredictionSchemeTransform transform)
            : base(attribute, transform)
        {

        }
        public override bool ComputeCorrectionValues(IntArray in_data, IntArray out_corr, int size, int num_components, int[] entry_to_point_id_map)
        {
            throw new NotImplementedException();
        }

        public override bool ComputeOriginalValues(IntArray in_corr, IntArray out_data, int size, int num_components,
            int[] entry_to_point_id_map)
        {

            this.transform_.InitializeDecoding(num_components);
            // Decode the original value for the first element.
            IntArray zero_vals = IntArray.Array(num_components);
            this.transform_.ComputeOriginalValue(zero_vals, in_corr, out_data);

            // Decode data from the front using D(i) = D(i) + D(i - 1).
            for (int i = num_components; i < size; i += num_components)
            {
                this.transform_.ComputeOriginalValue(out_data, i - num_components,
                    in_corr, i, out_data, i);
            }

            return true;
        }
    }
}
