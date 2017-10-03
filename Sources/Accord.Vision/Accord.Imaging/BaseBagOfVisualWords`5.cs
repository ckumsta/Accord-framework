﻿// Accord Imaging Library
// The Accord.NET Framework
// http://accord-framework.net
//
// Copyright © César Souza, 2009-2017
// cesarsouza at gmail.com
//
//    This library is free software; you can redistribute it and/or
//    modify it under the terms of the GNU Lesser General Public
//    License as published by the Free Software Foundation; either
//    version 2.1 of the License, or (at your option) any later version.
//
//    This library is distributed in the hope that it will be useful,
//    but WITHOUT ANY WARRANTY; without even the implied warranty of
//    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//    Lesser General Public License for more details.
//
//    You should have received a copy of the GNU Lesser General Public
//    License along with this library; if not, write to the Free Software
//    Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA
//

namespace Accord.Imaging
{
    using Accord.MachineLearning;
    using Accord.Math;
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Threading;
    using System.Linq;
    using Accord.Compat;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using Accord.Statistics;
    using Accord.Statistics.Distributions.Univariate;
    using System.Diagnostics;
    using Accord.Statistics.Distributions.Fitting;

    /// <summary>
    ///   Base class for <see cref="BagOfVisualWords">Bag of Visual Words</see> implementations.
    /// </summary>
    /// 
    /// <seealso cref="BagOfVisualWords"/>
    /// <seealso cref="BagOfVisualWords{TPoint, TFeature, TClustering, TDetector}"/>
    /// <seealso cref="BagOfVisualWords{TPoint, TFeature}"/>
    /// <seealso cref="BagOfVisualWords{TPoint}"/>
    /// 
    [Serializable]
    public class BaseBagOfVisualWords<TModel, TPoint, TFeature, TClustering, TDetector> :
        // TODO: Unify with Accord.MachineLearning.BaseBagOfWords
        ParallelLearningBase,
        IBagOfWords<string>,
        IBagOfWords<Bitmap>,
        IBagOfWords<UnmanagedImage>,
        IUnsupervisedLearning<TModel, string, int[]>,
        IUnsupervisedLearning<TModel, string, double[]>,
        IUnsupervisedLearning<TModel, Bitmap, int[]>,
        IUnsupervisedLearning<TModel, Bitmap, double[]>,
        IUnsupervisedLearning<TModel, UnmanagedImage, int[]>,
        IUnsupervisedLearning<TModel, UnmanagedImage, double[]>
        where TPoint : IFeatureDescriptor<TFeature>
        where TModel : BaseBagOfVisualWords<TModel, TPoint, TFeature, TClustering, TDetector>
        where TClustering : IUnsupervisedLearning<IClassifier<TFeature, int>, TFeature, int>
        where TDetector : IFeatureDetector<TPoint, TFeature>
    {

        private IClassifier<TFeature, int> classifier;

        /// <summary>
        ///   Gets the number of words in this codebook.
        /// </summary>
        /// 
        public int NumberOfWords { get; private set; }

        /// <summary>
        ///   Gets or sets the maximum number of descriptors that should be used 
        ///   to learn the codebook. Default is 0 (meaning to use all descriptors).
        /// </summary>
        /// 
        /// <value>The maximum number of samples.</value>
        /// 
        public int NumberOfDescriptors { get; set; }

        /// <summary>
        ///   Gets or sets the maximum number of descriptors per image that should be 
        ///   used to learn the codebook. Default is 0 (meaning to use all descriptors).
        /// </summary>
        /// 
        /// <value>The maximum number of samples per image.</value>
        /// 
        public int MaxDescriptorsPerImage { get; set; }

        /// <summary>
        ///   Gets the clustering algorithm used to create this model.
        /// </summary>
        /// 
        public TClustering Clustering { get; private set; }

        /// <summary>
        ///   Gets the <see cref="SpeededUpRobustFeaturesDetector">SURF</see>
        ///   feature point detector used to identify visual features in images.
        /// </summary>
        /// 
        public TDetector Detector { get; private set; }

        /// <summary>
        /// Gets the number of inputs accepted by the model.
        /// </summary>
        /// <value>The number of inputs.</value>
        public int NumberOfInputs
        {
            get { return -1; }
        }

        /// <summary>
        /// Gets the number of outputs generated by the model.
        /// </summary>
        /// <value>The number of outputs.</value>
        public int NumberOfOutputs
        {
            get { return NumberOfWords; }
        }

        /// <summary>
        /// Gets statistics about the last codebook learned.
        /// </summary>
        /// 
        public BagOfVisualWordsStatistics Statistics { get; private set; }

        /// <summary>
        ///   Constructs a new <see cref="BagOfVisualWords"/>.
        /// </summary>
        /// 
        protected BaseBagOfVisualWords()
        {
        }

        /// <summary>
        ///   Initializes this instance.
        /// </summary>
        /// 
        protected void Init(TDetector detector, TClustering algorithm)
        {
            this.Clustering = algorithm;
            this.Detector = detector;
        }

        internal KMeans KMeans(int numberOfWords)
        {
            return new KMeans(numberOfWords)
            {
                ComputeCovariances = false,
                UseSeeding = Seeding.KMeansPlusPlus,
                ParallelOptions = ParallelOptions
            };
        }


        #region Obsolete

        /// <summary>
        ///   Computes the Bag of Words model.
        /// </summary>
        /// 
        /// <param name="images">The set of images to initialize the model.</param>
        /// 
        /// <returns>The list of feature points detected in all images.</returns>
        /// 
        [Obsolete("Please use the Learn() method instead.")]
        public List<TPoint>[] Compute(Bitmap[] images)
        {
            var descriptors = new ConcurrentBag<TFeature>();
            var imagePoints = new List<TPoint>[images.Length];

            // For all images
            Parallel.For(0, images.Length, ParallelOptions,
                () => (IFeatureDetector<TPoint, TFeature>)Detector.Clone(),
                (i, state, detector) =>
                {
                    // Compute the feature points
                    IEnumerable<TPoint> points = detector.ProcessImage(images[i]);
                    foreach (IFeatureDescriptor<TFeature> point in points)
                        descriptors.Add(point.Descriptor);

                    imagePoints[i] = (List<TPoint>)points;
                    return detector;
                },
                (detector) => detector.Dispose());

            Learn(descriptors.ToArray());

            return imagePoints;
        }

        /// <summary>
        ///   Computes the Bag of Words model.
        /// </summary>
        /// 
        /// <param name="features">The extracted image features to initialize the model.</param>
        /// 
        /// <returns>The list of feature points detected in all images.</returns>
        /// 
        [Obsolete("Please use the Learn() method instead.")]
        public void Compute(TFeature[] features)
        {
            Learn(features);
        }

        /// <summary>
        ///   Computes the Bag of Words model.
        /// </summary>
        /// 
        /// <param name="images">The set of images to initialize the model.</param>
        /// <param name="threshold">Convergence rate for the k-means algorithm. Default is 1e-5.</param>
        /// 
        /// <returns>The list of feature points detected in all images.</returns>
        /// 
        [Obsolete("Please configure the tolerance of the clustering algorithm directly in the "
            + "algorithm itself by accessing it through the Clustering property of this class.")]
        public List<TPoint>[] Compute(Bitmap[] images, double threshold)
        {
            // Hack to maintain backwards compatibility
            var prop = Clustering.GetType().GetProperty("Tolerance");
            if (prop != null && prop.CanWrite)
                prop.SetValue(Clustering, threshold, null);

            return Compute(images);
        }

        /// <summary>
        ///   Gets the codeword representation of a given image.
        /// </summary>
        /// 
        /// <param name="value">The image to be processed.</param>
        /// 
        /// <returns>A double vector with the same length as words
        /// in the code book.</returns>
        /// 
        [Obsolete("Please use the Transform() method instead.")]
        public double[] GetFeatureVector(string value)
        {
            return Transform(value);
        }

        /// <summary>
        ///   Gets the codeword representation of a given image.
        /// </summary>
        /// 
        /// <param name="value">The image to be processed.</param>
        /// 
        /// <returns>A double vector with the same length as words
        /// in the code book.</returns>
        /// 
        [Obsolete("Please use the Transform() method instead.")]
        public double[] GetFeatureVector(Bitmap value)
        {
            return Transform(value);
        }

        /// <summary>
        ///   Gets the codeword representation of a given image.
        /// </summary>
        /// 
        /// <param name="value">The image to be processed.</param>
        /// 
        /// <returns>A double vector with the same length as words
        /// in the code book.</returns>
        /// 
        [Obsolete("Please use the Transform() method instead.")]
        public double[] GetFeatureVector(UnmanagedImage value)
        {
            return Transform(value);
        }

        /// <summary>
        ///   Gets the codeword representation of a given image.
        /// </summary>
        /// 
        /// <param name="points">The interest points of the image.</param>
        /// 
        /// <returns>A double vector with the same length as words
        /// in the code book.</returns>
        /// 
        [Obsolete("Please use the Transform() method instead.")]
        public double[] GetFeatureVector(List<TPoint> points)
        {
            return Transform(points);
        }

        /// <summary>
        ///   Saves the bag of words to a stream.
        /// </summary>
        /// 
        /// <param name="stream">The stream to which the bow is to be serialized.</param>
        /// 
        [Obsolete("Please use Accord.IO.Serializer.Save() instead (or use it as an extension method).")]
        public virtual void Save(Stream stream)
        {
            Accord.IO.Serializer.Save(this, stream);
        }

        /// <summary>
        ///   Saves the bag of words to a file.
        /// </summary>
        /// 
        /// <param name="path">The path to the file to which the bow is to be serialized.</param>
        /// 
        [Obsolete("Please use Accord.IO.Serializer.Save() instead (or use it as an extension method).")]

        public void Save(string path)
        {
            Accord.IO.Serializer.Save(this, path);
        }

        #endregion



        #region Transform

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[] Transform(IList<TPoint> input, int[] result)
        {
            // Detect all activation centroids
            Parallel.For(0, input.Count, ParallelOptions, i =>
            {
                TFeature x = input[i].Descriptor;
                int j = classifier.Decide(x);
                Interlocked.Increment(ref result[j]);
            });

            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[] Transform(IList<TPoint> input, double[] result)
        {
            // Detect all activation centroids
            Parallel.For(0, input.Count, ParallelOptions, i =>
            {
                TFeature x = input[i].Descriptor;
                int j = classifier.Decide(x);
                InterlockedEx.Increment(ref result[j]);
            });

            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[] Transform(string input, double[] result)
        {
            using (Bitmap bmp = Accord.Imaging.Image.FromFile(input))
                return Transform((List<TPoint>)Detector.ProcessImage(bmp), result);
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[] Transform(string input, int[] result)
        {
            using (Bitmap bmp = Accord.Imaging.Image.FromFile(input))
                return Transform((List<TPoint>)Detector.ProcessImage(bmp), result);
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[] Transform(Bitmap input, double[] result)
        {
            return Transform((List<TPoint>)Detector.ProcessImage(input), result);
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[] Transform(Bitmap input, int[] result)
        {
            return Transform((List<TPoint>)Detector.ProcessImage(input), result);
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[] Transform(UnmanagedImage input, double[] result)
        {
            return Transform((List<TPoint>)Detector.ProcessImage(input), result);
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[] Transform(UnmanagedImage input, int[] result)
        {
            return Transform((List<TPoint>)Detector.ProcessImage(input), result);
        }


        /// <summary>
        /// Applies the transformation to an input, producing an associated output.
        /// </summary>
        /// <param name="input">The input data to which the transformation should be applied.</param>
        /// <returns>The output generated by applying this transformation to the given input.</returns>
        public double[] Transform(List<TPoint> input)
        {
            return Transform(input, new double[NumberOfWords]);
        }

        /// <summary>
        /// Applies the transformation to an input, producing an associated output.
        /// </summary>
        /// <param name="input">The input data to which the transformation should be applied.</param>
        /// <returns>The output generated by applying this transformation to the given input.</returns>
        public double[] Transform(string input)
        {
            return Transform(input, new double[NumberOfWords]);
        }

        int[] ITransform<string, int[]>.Transform(string input)
        {
            return Transform(input, new int[NumberOfWords]);
        }

        /// <summary>
        /// Applies the transformation to an input, producing an associated output.
        /// </summary>
        /// <param name="input">The input data to which the transformation should be applied.</param>
        /// <returns>The output generated by applying this transformation to the given input.</returns>
        public double[] Transform(Bitmap input)
        {
            return Transform(input, new double[NumberOfWords]);
        }

        int[] ITransform<Bitmap, int[]>.Transform(Bitmap input)
        {
            return Transform(input, new int[NumberOfWords]);
        }

        /// <summary>
        /// Applies the transformation to an input, producing an associated output.
        /// </summary>
        /// <param name="input">The input data to which the transformation should be applied.</param>
        /// <returns>The output generated by applying this transformation to the given input.</returns>
        public double[] Transform(UnmanagedImage input)
        {
            return Transform(input, new double[NumberOfWords]);
        }


        int[] ITransform<UnmanagedImage, int[]>.Transform(UnmanagedImage input)
        {
            return Transform(input, new int[NumberOfWords]);
        }



        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(string[] input)
        {
            return Transform(input, Jagged.Zeros(input.Length, NumberOfWords));
        }

        int[][] ITransform<string, int[]>.Transform(string[] input)
        {
            return Transform(input, Jagged.Zeros<int>(input.Length, NumberOfWords));
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(Bitmap[] input)
        {
            return Transform(input, Jagged.Zeros(input.Length, NumberOfWords));
        }

        int[][] ITransform<Bitmap, int[]>.Transform(Bitmap[] input)
        {
            return Transform(input, Jagged.Zeros<int>(input.Length, NumberOfWords));
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(UnmanagedImage[] input)
        {
            return Transform(input, Jagged.Zeros<double>(input.Length, NumberOfWords));
        }

        int[][] ITransform<UnmanagedImage, int[]>.Transform(UnmanagedImage[] input)
        {
            return Transform(input, Jagged.Zeros<int>(input.Length, NumberOfWords));
        }



        /// <summary>
        ///   Executes a parallel for using the feature detector in a thread-safe way.
        /// </summary>
        private void For(int fromInclusive, int toExclusive,
            Action<int, IFeatureDetector<TPoint, TFeature>> action)
        {
            if (ParallelOptions.MaxDegreeOfParallelism == 1)
            {
                for (int i = fromInclusive; i < toExclusive; i++)
                    action(i, Detector);
                return;
            }

            Parallel.For(fromInclusive, toExclusive, ParallelOptions,

                // If we don't clone the detector, we run in race conditions
                () => (IFeatureDetector<TPoint, TFeature>)Detector.Clone(),

                (i, state, detector) =>
                {
                    // here, each thread has its own copy of the detector
                    action(i, detector);
                    return detector;
                },

                (detector) => detector.Dispose());
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(string[] input, double[][] result)
        {
            For(0, input.Length, (i, detector) =>
            {
                using (Bitmap bmp = Accord.Imaging.Image.FromFile(input[i]))
                    Transform((IList<TPoint>)detector.ProcessImage(bmp), result[i]);
            });

            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[][] Transform(string[] input, int[][] result)
        {
            For(0, input.Length, (i, detector) =>
            {
                using (Bitmap bmp = Accord.Imaging.Image.FromFile(input[i]))
                    Transform((IList<TPoint>)detector.ProcessImage(bmp), result[i]);
            });

            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(Bitmap[] input, double[][] result)
        {
            For(0, input.Length, (i, detector) =>
                Transform((IList<TPoint>)detector.ProcessImage(input[i]), result[i]));
            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[][] Transform(Bitmap[] input, int[][] result)
        {
            For(0, input.Length, (i, detector) =>
                 Transform((IList<TPoint>)detector.ProcessImage(input[i]), result[i]));

            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public int[][] Transform(UnmanagedImage[] input, int[][] result)
        {
            For(0, input.Length, (i, detector) =>
                 Transform((IList<TPoint>)detector.ProcessImage(input[i]), result[i]));

            return result;
        }

        /// <summary>
        /// Applies the transformation to a set of input vectors,
        /// producing an associated set of output vectors.
        /// </summary>
        /// <param name="input">The input data to which
        /// the transformation should be applied.</param>
        /// <param name="result">The location to where to store the
        /// result of this transformation.</param>
        /// <returns>The output generated by applying this
        /// transformation to the given input.</returns>
        public double[][] Transform(UnmanagedImage[] input, double[][] result)
        {
            For(0, input.Length, (i, detector) =>
                 Transform((IList<TPoint>)detector.ProcessImage(input[i]), result[i]));

            return result;
        }

        #endregion



        #region Learn
        /// <summary>
        /// Learns a model that can map the given inputs to the desired outputs.
        /// </summary>
        /// <param name="x">The model inputs.</param>
        /// <param name="weights">The weight of importance for each input sample.</param>
        /// <returns>A model that has learned how to produce suitable outputs
        /// given the input data <paramref name="x" />.</returns>
        public TModel Learn(TFeature[] x, double[] weights = null)
        {
            if (weights != null && x.Length != weights.Length)
                throw new DimensionMismatchException("weights", "The weights vector should have the same length as x.");

            if (x.Length <= NumberOfWords)
            {
                throw new InvalidOperationException("Not enough data points to cluster. Please try "
                    + "to adjust the feature extraction algorithm to generate more points.");
            }

            this.Statistics = new BagOfVisualWordsStatistics()
            {
                TotalNumberOfDescriptors = x.Length,
            };

            return learn(x, weights);
        }

        /// <summary>
        /// Learns a model that can map the given inputs to the desired outputs.
        /// </summary>
        /// <param name="x">The model inputs.</param>
        /// <param name="weights">The weight of importance for each input sample.</param>
        /// <returns>A model that has learned how to produce suitable outputs
        /// given the input data <paramref name="x" />.</returns>
        public TModel Learn(string[] x, double[] weights = null)
        {
            return learn(x, weights, (xi, detector) =>
            {
                using (Bitmap bmp = Accord.Imaging.Image.FromFile(xi))
                    return detector.ProcessImage(bmp);
            });
        }

        /// <summary>
        /// Learns a model that can map the given inputs to the desired outputs.
        /// </summary>
        /// <param name="x">The model inputs.</param>
        /// <param name="weights">The weight of importance for each input sample.</param>
        /// <returns>A model that has learned how to produce suitable outputs
        /// given the input data <paramref name="x" />.</returns>
        public TModel Learn(Bitmap[] x, double[] weights = null)
        {
            return learn(x, weights, (xi, detector) => detector.ProcessImage(xi));
        }

        /// <summary>
        /// Learns a model that can map the given inputs to the desired outputs.
        /// </summary>
        /// <param name="x">The model inputs.</param>
        /// <param name="weights">The weight of importance for each input sample.</param>
        /// <returns>A model that has learned how to produce suitable outputs
        /// given the input data <paramref name="x" />.</returns>
        public TModel Learn(UnmanagedImage[] x, double[] weights = null)
        {
            return learn(x, weights, (xi, detector) => detector.ProcessImage(xi));
        }

        private TModel learn<TInput>(TInput[] x, double[] weights,
            Func<TInput, IFeatureDetector<TPoint, TFeature>, IEnumerable<TPoint>> action)
        {
            var descriptorsPerImage = new TFeature[x.Length][];
            var totalDescriptorCounts = new double[x.Length];
            int takenDescriptorCount = 0;

            // For all images
            For(0, x.Length, (i, detector) =>
            {
                if (NumberOfDescriptors > 0 && takenDescriptorCount >= NumberOfDescriptors)
                    return;

                TFeature[] desc = action(x[i], detector).Select(p => p.Descriptor).ToArray();

                totalDescriptorCounts[i] = desc.Length;

                if (MaxDescriptorsPerImage > 0)
                    desc = desc.Sample(MaxDescriptorsPerImage);

                Interlocked.Add(ref takenDescriptorCount, desc.Length);

                descriptorsPerImage[i] = desc;
            });

            if (NumberOfDescriptors >= 0 && takenDescriptorCount < NumberOfDescriptors)
            {
                throw new InvalidOperationException("There were not enough descriptors to sample the desired amount " +
                    "of samples ({0}). Please either increase the number of images, or increase the number of ".Format(NumberOfDescriptors) +
                    "descriptors that are sampled from each image by adjusting the MaxSamplesPerImage property ({0}).".Format(MaxDescriptorsPerImage));
            }

            var totalDescriptors = new TFeature[takenDescriptorCount];
            var totalWeights = weights != null ? new double[takenDescriptorCount] : null;
            int[] imageIndices = new int[takenDescriptorCount];

            int c = 0, w = 0;
            for (int i = 0; i < descriptorsPerImage.Length; i++)
            {
                if (descriptorsPerImage[i] != null)
                {
                    if (weights != null)
                        totalWeights[w++] = weights[i];
                    for (int j = 0; j < descriptorsPerImage[i].Length; j++)
                    {
                        totalDescriptors[c] = descriptorsPerImage[i][j];
                        imageIndices[c] = i;
                        c++;
                    }
                }
            }

            if (NumberOfDescriptors > 0)
            {
                int[] idx = Vector.Sample(NumberOfDescriptors);
                totalDescriptors = totalDescriptors.Get(idx);
                imageIndices = imageIndices.Get(idx);
            }

            int[] hist = imageIndices.Histogram();

            Debug.Assert(hist.Sum() == (NumberOfDescriptors > 0 ? NumberOfDescriptors : takenDescriptorCount));

            this.Statistics = new BagOfVisualWordsStatistics()
            {
                TotalNumberOfImages = x.Length,
                TotalNumberOfDescriptors = (int)totalDescriptorCounts.Sum(),
                TotalNumberOfDescriptorsPerImage = NormalDistribution.Estimate(totalDescriptorCounts, new NormalOptions { Robust = true }),
                TotalNumberOfDescriptorsPerImageRange = new IntRange((int)totalDescriptorCounts.Min(), (int)totalDescriptorCounts.Max()),

                NumberOfImagesTaken = hist.Length,
                NumberOfDescriptorsTaken = totalDescriptors.Length,
                NumberOfDescriptorsTakenPerImage = NormalDistribution.Estimate(hist.ToDouble(), new NormalOptions { Robust = true }),
                NumberOfDescriptorsTakenPerImageRange = new IntRange(hist.Min(), hist.Max())
            };

            return learn(totalDescriptors, totalWeights);
        }

        private TModel learn(TFeature[] x, double[] weights)
        {
            this.classifier = this.Clustering.Learn(x, weights);
            this.NumberOfWords = this.classifier.NumberOfClasses;

            return (TModel)this;
        }
        #endregion

        int ITransform.NumberOfInputs
        {
            get { return NumberOfInputs; }
            set { throw new InvalidOperationException("This property is read-only."); }
        }

        int ITransform.NumberOfOutputs
        {
            get { return NumberOfOutputs; }
            set { throw new InvalidOperationException("This property is read-only."); }
        }
    }
}
