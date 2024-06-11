﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Openize.Draco.Encoder;
using Openize.Draco.Utils;

namespace Openize.Draco
{
    enum AttributeTransformType
    {
        InvalidTransform = -1,
        NoTransform = 0,
        QuantizationTransform = 1,
        OctahedronTransform = 2
    }
    class AttributeTransformData
    {
        public AttributeTransformType transformType = AttributeTransformType.InvalidTransform;

        private DataBuffer dataBuffer = new DataBuffer();

        public int GetInt(int byteOffset)
        {
            return dataBuffer.ReadInt(byteOffset);
        }

        public float GetFloat(int byteOffset)
        {
            return dataBuffer.ReadFloat(byteOffset);
        }

        public void AppendValue(int value)
        {
            dataBuffer.Write(dataBuffer.Length, value);
        }
        public void AppendValue(float value)
        {
            dataBuffer.Write(dataBuffer.Length, value);
        }

    }

    abstract class AttributeTransform
    {

        // Return attribute transform type.
        public abstract AttributeTransformType Type();

        // Try to init transform from attribute.
        public abstract bool InitFromAttribute(PointAttribute attribute);

        // Copy parameter values into the provided AttributeTransformData instance.
        public abstract void CopyToAttributeTransformData(AttributeTransformData outData);

        public bool TransferToAttribute(PointAttribute attribute)
        {
            var transform_data = new AttributeTransformData();
            CopyToAttributeTransformData(transform_data);
            attribute.AttributeTransformData = transform_data;
            return true;
        }

        protected PointAttribute InitPortableAttribute(int num_entries, int num_components, int num_points,
            PointAttribute attribute, bool is_unsigned)
        {
            DataType dt = is_unsigned ? DataType.UINT32 : DataType.INT32;
            var portable_attribute = new PointAttribute();
            portable_attribute.AttributeType = attribute.AttributeType;
            portable_attribute.ComponentsCount = num_components;
            portable_attribute.DataType = dt;
            portable_attribute.ByteStride = num_components * DracoUtils.DataTypeLength(dt);
            portable_attribute.Reset(num_entries);
            if (num_points > 0)
            {
                portable_attribute.SetExplicitMapping(num_points);
            }
            else
            {
                portable_attribute.IdentityMapping = true;
            }

            return portable_attribute;
        }
    }

    class AttributeQuantizationTransform : AttributeTransform
    {

        public int quantization_bits_ = -1;

        // Minimal dequantized value for each component of the attribute.
        public float[] min_values_;

        // Bounds of the dequantized attribute (max delta over all components).
        public float range_;

        public override AttributeTransformType Type()
        {
            return AttributeTransformType.QuantizationTransform;
        }

        public override bool InitFromAttribute(PointAttribute attribute)
        {
            AttributeTransformData transform_data =
                attribute.AttributeTransformData;
            if (transform_data == null || transform_data.transformType != AttributeTransformType.QuantizationTransform)
                return DracoUtils.Failed(); // Wrong transform type.
            int byte_offset = 0;
            quantization_bits_ = transform_data.GetInt(byte_offset);
            byte_offset += 4;
            min_values_ = new float[attribute.ComponentsCount];
            for (int i = 0; i < attribute.ComponentsCount; ++i)
            {
                min_values_[i] = transform_data.GetFloat(byte_offset);
                byte_offset += 4;
            }

            range_ = transform_data.GetFloat(byte_offset);
            return true;
        }

// Copy parameter values into the provided AttributeTransformData instance.
        public override void CopyToAttributeTransformData(AttributeTransformData out_data)
        {
            out_data.transformType = AttributeTransformType.QuantizationTransform;
            out_data.AppendValue(quantization_bits_);
            for (int i = 0; i < min_values_.Length; ++i)
            {
                out_data.AppendValue(min_values_[i]);
            }

            out_data.AppendValue(range_);
        }

        public void SetParameters(int quantization_bits, float[] min_values, int num_components, float range)
        {
            quantization_bits_ = quantization_bits;
            this.min_values_ = (float[])min_values.Clone();
            range_ = range;
        }

        public bool ComputeParameters(PointAttribute attribute, int quantization_bits)
        {
            if (quantization_bits_ != -1)
            {
                return DracoUtils.Failed(); // already initialized.
            }

            quantization_bits_ = quantization_bits;

            int num_components = attribute.ComponentsCount;
            range_ = 0.0f;
            min_values_ = new float[num_components];
            float[] max_values = new float[num_components];
            float[] att_val = new float[num_components];
            // Compute minimum values and max value difference.
            attribute.GetValue(0, att_val);
            attribute.GetValue(0, min_values_);
            attribute.GetValue(0, max_values);

            for (int i = 1; i < attribute.NumUniqueEntries; ++i)
            {
                attribute.GetValue(i, att_val);
                for (int c = 0; c < num_components; ++c)
                {
                    if (min_values_[c] > att_val[c])
                        min_values_[c] = att_val[c];
                    if (max_values[c] < att_val[c])
                        max_values[c] = att_val[c];
                }
            }

            for (int c = 0; c < num_components; ++c)
            {
                float dif = max_values[c] - min_values_[c];
                if (dif > range_)
                    range_ = dif;
            }

            if (DracoUtils.IsZero(range_))
                range_ = 1.0f;

            return true;
        }

        public bool EncodeParameters(EncoderBuffer encoder_buffer)
        {
            if (quantization_bits_ != -1)
            {
                encoder_buffer.Encode(min_values_);
                encoder_buffer.Encode(range_);
                encoder_buffer.Encode((byte)quantization_bits_);
                return true;
            }

            return DracoUtils.Failed();
        }

        public PointAttribute GeneratePortableAttribute(PointAttribute attribute, int num_points)
        {

            // Allocate portable attribute.
            int num_entries = num_points;
            int num_components = attribute.ComponentsCount;
            PointAttribute portable_attribute = InitPortableAttribute(num_entries, num_components, 0, attribute, true);

            // Quantize all values using the order given by point_ids.
            int offset = portable_attribute.ByteOffset;

            int max_quantized_value = (1 << (quantization_bits_)) - 1;
            Quantizer quantizer = new Quantizer(range_, max_quantized_value);
            //int dst_index = 0;
            float[] att_val = new float[num_components];
            for (int i = 0; i < num_points; ++i)
            {
                int att_val_id = attribute.MappedIndex(i);
                attribute.GetValue(att_val_id, att_val);
                for (int c = 0; c < num_components; ++c)
                {
                    float value = (att_val[c] - min_values_[c]);
                    int q_val = quantizer.QuantizeFloat(value);
                    portable_attribute.Buffer.Write(offset, q_val);
                    offset += 4;
                }
            }

            return portable_attribute;
        }

        public PointAttribute GeneratePortableAttribute(PointAttribute attribute, int[] point_ids, int num_points)
        {
            // Allocate portable attribute.
            int num_entries = point_ids.Length;
            int num_components = attribute.ComponentsCount;
            PointAttribute portable_attribute = InitPortableAttribute(
                num_entries, num_components, num_points, attribute, true);

            // Quantize all values using the order given by point_ids.
            int offset = portable_attribute.ByteOffset;
            int max_quantized_value = (1 << (quantization_bits_)) - 1;
            Quantizer quantizer = new Quantizer(range_, max_quantized_value);
            //int dst_index = 0;
            float[] att_val = new float[num_components];
            for (int i = 0; i < point_ids.Length; ++i)
            {
                int att_val_id = attribute.MappedIndex(point_ids[i]);
                attribute.GetValue(att_val_id, att_val);
                for (int c = 0; c < num_components; ++c)
                {
                    float value = (att_val[c] - min_values_[c]);
                    int q_val = quantizer.QuantizeFloat(value);
                    portable_attribute.Buffer.Write(offset, q_val);
                    offset += 4;
                }
            }

            return portable_attribute;
        }
    }
}
