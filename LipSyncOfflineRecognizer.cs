using UnityEngine;
using System.Threading;
using System.Collections;
using System;


namespace LipSyncLite
{

    public class LipSyncOfflineRecognizer
    {
        private const int FILTER_SIZE = 7;
        private const float FILTER_DEVIATION_SQUARE = 5.0f;
        private const int FORMANT_COUNT = 1;

        private ERecognizerLanguage recognizingLanguage;

        private int windowSize;
        private int shiftStepSize;
        private float amplitudeThreshold;
        private float[] gaussianFilter;
        private float[] windowArray;

        private float amplitudeSum;
        private float[] smoothedAudioSpectrum;
        private float[] peakValues;
        private int[] peakPositions;

        private float frequencyUnit;
        private float[] formantArray;

        private string[] currentVowels;
        private float[] currentVowelFormantCeilValues;


        // TODO: Data-lization
        private string[] vowelsByFormantJP = { "i", "u", "e", "o", "a" };
        private float[] vowelFormantFloorJP = { 0.0f, 250.0f, 300.0f, 450.0f, 600.0f };
        private string[] vowelsByFormantCN = { "i", "v", "u", "e", "o", "a" };
        private float[] vowelFormantFloorCN = { 0.0f, 100.0f, 250.0f, 300.0f, 450.0f, 600.0f };

        public LipSyncOfflineRecognizer(ERecognizerLanguage recognizingLanguage, float amplitudeThreshold, int windowSize, int shiftStepSize)
        {
            this.recognizingLanguage = recognizingLanguage;
            this.windowSize = Mathf.ClosestPowerOfTwo(windowSize);
            this.shiftStepSize = shiftStepSize;
            
            this.amplitudeThreshold = amplitudeThreshold;
            this.gaussianFilter = MathToolBox.GenerateGaussianFilter(FILTER_SIZE, FILTER_DEVIATION_SQUARE);
            //GenerateWindow汉明窗的窗口函数生成，将其存储在windowArray中
            this.windowArray = MathToolBox.GenerateWindow(windowSize, MathToolBox.EWindowType.Hamming);

            this.smoothedAudioSpectrum = new float[this.windowSize];
            this.peakValues = new float[FORMANT_COUNT];
            this.peakPositions = new int[FORMANT_COUNT];
            this.formantArray = new float[FORMANT_COUNT];

            //currentAudioSpectrum = new float[this.windowSize];


        }

        /// <summary>
        /// 初始化Thread类的新实例
        /// </summary>
        /// <param name="start">有参委托对象</param>

        //声明一个委托
        public delegate float[] AddHandler(int start,int size, float[] b);
       // public interface IAsyncResult
        static float[] th_return(int start,int size,float[] b)
        {
            return MathToolBox.DiscreteCosineTransform(start,size,b);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="audioClip"></param>
        /// <returns></returns>
        public string[] RecognizeAllByAudioClip(AudioClip audioClip)
        {
            //audioClip.samples原音频数据的总采样数量，shiftStepSize预先设定的步进长度
            //recognizeSampleCount分帧数量
            //CeilToInt,将float类型数据转化为int类型
            int recognizeSampleCount = Mathf.CeilToInt((float)(audioClip.samples) / (float)(shiftStepSize));
            string[] result = new string[recognizeSampleCount];

            AddHandler task1 = th_return;



            float[] currentAudioData = new float[this.windowSize];
            float[] currentAudioSpectrum = new float[this.windowSize];
            float[] c1 = new float[this.windowSize / 4];
            float[] c2 = new float[this.windowSize / 4];
            float[] c3 = new float[this.windowSize / 4];
            float[] c4 = new float[this.windowSize-(this.windowSize / 4)*3];
            float[] r1 = new float[this.windowSize / 4];
            float[] r2 = new float[this.windowSize / 4];
            float[] r3 = new float[this.windowSize / 4];
            float[] r4 = new float[this.windowSize - (this.windowSize / 4) * 3];


            for (int i = 0; i < recognizeSampleCount; ++i)
            {
                //让currentAudioData存贮根据步进长度与分帧数量，从原音频中截取的数据帧
                audioClip.GetData(currentAudioData, i * shiftStepSize);//audioClip,unity自带函数
                for (int j = 0; j < windowSize; ++j)
                {
                    currentAudioData[j] *= windowArray[j];
                    
                }
                // currentAudioSpectrum = MathToolBox.DiscreteCosineTransform(currentAudioData);
                // currentAudioSpectrum = MathToolBox.fft_frequency(currentAudioData, currentAudioData.Length-1);

                IAsyncResult asyncResult1 = task1.BeginInvoke(0,c1.Length, currentAudioData, null, null);
                IAsyncResult asyncResult2 = task1.BeginInvoke(c1.Length,c2.Length, currentAudioData, null, null);
                IAsyncResult asyncResult3 = task1.BeginInvoke(c1.Length+c2.Length,c3.Length, currentAudioData, null, null);
                IAsyncResult asyncResult4 = task1.BeginInvoke(c1.Length+c2.Length+c3.Length,c4.Length, currentAudioData, null, null);
                
                r1 = task1.EndInvoke(asyncResult1);
                r2 = task1.EndInvoke(asyncResult2);
                r3 = task1.EndInvoke(asyncResult3);
                r4 = task1.EndInvoke(asyncResult4);

                for (int j = 0; j < windowSize; ++j)
                {
                    if (j < windowSize / 4) currentAudioSpectrum[j] = r1[j];
                    else if (j < windowSize / 2) currentAudioSpectrum[j] = r2[j - windowSize / 4];
                    else if (j < (windowSize / 4) * 3) currentAudioSpectrum[j] = r3[j - windowSize / 2];
                    else currentAudioSpectrum[j] = r4[j - (windowSize / 4) * 3];
                }
                // thr.Start((object)currentAudioData);
                //  thr.Join();

                amplitudeSum = 0.0f;
                for (int k = 0; k < windowSize; ++k)
                {
                    amplitudeSum += currentAudioSpectrum[k];
                }

                if (amplitudeSum >= amplitudeThreshold)
                {
                    MathToolBox.Convolute(currentAudioSpectrum, gaussianFilter, MathToolBox.EPaddleType.Repeat, smoothedAudioSpectrum);
                    MathToolBox.FindLocalLargestPeaks(smoothedAudioSpectrum, peakValues, peakPositions);
                    frequencyUnit = audioClip.frequency / 2 / windowSize;
                    for (int l = 0; l < formantArray.Length; ++l)
                    {
                        formantArray[l] = peakPositions[l] * frequencyUnit;
                    }

                    switch (recognizingLanguage)
                    {
                        case ERecognizerLanguage.Japanese:
                            currentVowels = vowelsByFormantJP;
                            currentVowelFormantCeilValues = vowelFormantFloorJP;
                            break;
                        case ERecognizerLanguage.Chinese:
                            currentVowels = vowelsByFormantCN;
                            currentVowelFormantCeilValues = vowelFormantFloorCN;
                            break;
                    }
                    for (int m = 0; m < currentVowelFormantCeilValues.Length; ++m)
                    {
                        if (formantArray[0] > currentVowelFormantCeilValues[m])
                        {
                            result[i] = currentVowels[m];
                        }
                    }
                }
                else
                {
                    result[i] = null;
                }
            }

            return result;
        }

    }

}
