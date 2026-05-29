using System;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// ChunityFFT — Cooley-Tukey FFT
// Giữ nguyên logic từ code gốc (Stanford CCRMA / Chunity)
// ─────────────────────────────────────────────────────────────────────────────
public class ChunityFFT
{
    public static void ComputeFFT(Complex[] signal, bool reverse)
    {
        int power = (int)(Math.Log(signal.Length, 2) + 0.5);
        int count = 1;
        for (int i = 0; i < power; i++) count <<= 1;

        int mid = count >> 1, j = 0;
        for (int i = 0; i < count - 1; i++)
        {
            if (i < j) { var tmp = signal[i]; signal[i] = signal[j]; signal[j] = tmp; }
            int k = mid;
            while (k <= j) { j -= k; k >>= 1; }
            j += k;
        }

        Complex r = new Complex(-1, 0);
        int l2 = 1;
        for (int l = 0; l < power; l++)
        {
            int l1 = l2; l2 <<= 1;
            Complex r2 = new Complex(1, 0);
            for (int n = 0; n < l1; n++)
            {
                for (int i = n; i < count; i += l2)
                {
                    int     i1  = i + l1;
                    Complex tmp = r2 * signal[i1];
                    signal[i1] = signal[i] - tmp;
                    signal[i] += tmp;
                }
                r2 = r2 * r;
            }
            r.img = Math.Sqrt((1d - r.real) / 2d);
            if (!reverse) r.img = -r.img;
            r.real = Math.Sqrt((1d + r.real) / 2d);
        }

        if (!reverse)
        {
            double scale = 1d / count;
            for (int i = 0; i < count; i++) signal[i] *= scale;
        }
    }

    public static void ComputeFFT(Complex[] signal, bool reverse, float[] magBins)
    {
        ComputeFFT(signal, reverse);
        int half = signal.Length / 2;
        if (!reverse)
            for (int i = 0; i < half; i++) magBins[i] = signal[i].fMagnitude;
        else
            for (int i = 0; i < half; i++) magBins[i] = Math.Sign(signal[i].real) * signal[i].fMagnitude;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Windowing
// ─────────────────────────────────────────────────────────────────────────────
public struct Windowing
{
    public static void Apply(float[] signal, float[] window)
    {
        for (int i = 0; i < window.Length; i++) signal[i] *= window[i];
        for (int i = window.Length; i < signal.Length; i++) signal[i] = 0f;
    }

    public static float[] Hanning(int size)
    {
        float[] w = new float[size];
        float delta = 2 * Mathf.PI / size;
        for (int i = 0; i < size; i++)
            w[i] = 0.5f * (1f - Mathf.Cos(delta * i));
        return w;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Complex — giữ nguyên từ code gốc
// ─────────────────────────────────────────────────────────────────────────────
public struct Complex
{
    public double real, img;

    public Complex(double aReal, double aImg) { real = aReal; img = aImg; }

    public static Complex FromAngle(double angle, double mag) =>
        new Complex(Math.Cos(angle) * mag, Math.Sin(angle) * mag);

    public static void Float2Complex(float[] input, Complex[] result)
    {
        for (int i = 0; i < input.Length; i++)
            result[i] = new Complex(input[i], 0);
    }

    public Complex conjugate    => new Complex(real, -img);
    public double  magnitude    => Math.Sqrt(real * real + img * img);
    public float   fMagnitude   => (float)Math.Sqrt(real * real + img * img);
    public float   fReal        { get => (float)real; set => real = value; }
    public float   fImg         { get => (float)img;  set => img  = value; }

    public static Complex operator +(Complex a, Complex b) => new Complex(a.real + b.real, a.img + b.img);
    public static Complex operator -(Complex a, Complex b) => new Complex(a.real - b.real, a.img - b.img);
    public static Complex operator -(Complex a)            => new Complex(-a.real, -a.img);
    public static Complex operator *(Complex a, Complex b) =>
        new Complex(a.real * b.real - a.img * b.img, a.real * b.img + a.img * b.real);
    public static Complex operator *(Complex a, double b)  => new Complex(a.real * b, a.img * b);
    public static Complex operator /(Complex a, double b)  => new Complex(a.real / b, a.img / b);
    public static Complex operator /(Complex a, Complex b)
    {
        double d = 1d / (b.real * b.real + b.img * b.img);
        return new Complex((a.real * b.real + a.img * b.img) * d, (-a.real * b.img + a.img * b.real) * d);
    }
}
