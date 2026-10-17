"""Sinh âm thanh TẠM (placeholder) cho game bằng numpy: SFX (WAV mono 44,1 kHz) + 3 vòng nhạc nền (OGG stereo).

Chạy:   python3 tools/gen_audio.py [--only UiTap CoinPay Day ...] [--no-music]
Cần:    python3 + numpy; ffmpeg (có encoder 'vorbis') chỉ để xuất nhạc OGG.
Ra:     game/Assets/_Project/Art/Audio/Sfx/AU_Sfx_<Id>[_n].wav
        game/Assets/_Project/Art/Audio/Bgm/AU_Bgm_<Id>.ogg
Sau đó: trong Unity chạy menu  Cafe/Audio/Ensure Audio Library  (dựng AudioLibrary.asset từ các file này).

Chất lượng: đủ nghe thử, đồng bộ một gam ngũ cung Rê trưởng (D E F# A B) để các âm cao độ luôn hoà nhau. Âm "hữu cơ"
(rót, khuấy, lau) và nhạc nền chỉ là giả lập, hãy thay bằng file CC0 / tự thu: đặt đúng tên AU_Sfx_<Id>[_n] / AU_Bgm_<Id>
rồi chạy lại menu Ensure Audio Library (không phải sửa code). Id phải trùng enum Sfx / Bgm trong Scripts/Audio/Sfx.cs.
"""
import argparse
import subprocess
import sys
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
SFX_DIR = ROOT / "game" / "Assets" / "_Project" / "Art" / "Audio" / "Sfx"
BGM_DIR = ROOT / "game" / "Assets" / "_Project" / "Art" / "Audio" / "Bgm"
SR = 44100
MUSIC_SR = 22050
PEAK = 0.85

# Gam ngũ cung Rê trưởng (Hz)
D4, E4, F4s, A4, B4 = 293.66, 329.63, 369.99, 440.00, 493.88
D5, E5, F5s, A5, B5 = 587.33, 659.25, 739.99, 880.00, 987.77
D6, E6, F6s, A6 = 1174.66, 1318.51, 1479.98, 1760.00
PENTA = [D4, E4, F4s, A4, B4, D5, E5, F5s, A5, B5, D6]


# ------------------------------------------------------------------ khối xây dựng
def tt(dur, sr=SR):
    return np.arange(int(dur * sr)) / sr


def env_exp(t, decay, attack=0.002):
    """Bao hình: lên nhanh (attack) rồi tắt hàm mũ."""
    a = np.minimum(1.0, t / max(attack, 1e-4))
    return a * np.exp(-t * decay)


def sine(freq, t, phase=0.0):
    return np.sin(2 * np.pi * freq * t + phase)


def bell(freq, dur, decay=4.0, sr=SR, amp=1.0):
    """Chuông: các hài phi hoà 1 / 2.76 / 5.4 / 8.93, tắt nhanh dần ở hài cao."""
    t = tt(dur, sr)
    y = np.zeros_like(t)
    for ratio, a, d in ((1.0, 1.0, 1.0), (2.76, 0.45, 1.6), (5.4, 0.2, 2.4), (8.93, 0.08, 3.5)):
        if freq * ratio < sr * 0.45:
            y += a * sine(freq * ratio, t) * env_exp(t, decay * d)
    return amp * y


def glide(f0, f1, dur, sr=SR):
    """Sin quét tần số hàm mũ f0 -> f1."""
    t = tt(dur, sr)
    k = np.log(f1 / f0) / max(dur, 1e-6)
    phase = 2 * np.pi * f0 * (np.exp(k * t) - 1) / k if abs(k) > 1e-6 else 2 * np.pi * f0 * t
    return np.sin(phase)


def noise(dur, rng, sr=SR):
    return rng.uniform(-1, 1, int(dur * sr))


def band(x, lo, hi, sr=SR):
    """Lọc dải bằng mặt nạ FFT (đủ cho âm ngắn)."""
    spec = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / sr)
    spec[(f < lo) | (f > hi)] = 0
    return np.fft.irfft(spec, len(x))


def lowpass(x, cutoff, sr=SR):
    return band(x, 0, cutoff, sr)


def pluck(freq, dur, sr=SR, decay=0.9965, rng=None):
    """Karplus-Strong: dây gảy (ấm hơn nếu hạ decay)."""
    rng = rng or np.random.default_rng(1)
    n = max(2, int(sr / freq))
    total = int(dur * sr)
    buf = rng.uniform(-1, 1, n)
    out = np.empty(total)
    idx = 0
    for i in range(total):
        out[i] = buf[idx]
        nxt = (idx + 1) % n
        buf[idx] = decay * 0.5 * (buf[idx] + buf[nxt])
        idx = nxt
    return out


def place(dst, src, at, sr=SR):
    """Cộng src vào dst bắt đầu ở giây `at` (cắt phần thừa)."""
    i = int(at * sr)
    if i >= len(dst):
        return
    n = min(len(src), len(dst) - i)
    dst[i:i + n] += src[:n]


def fade_out(x, ms=8, sr=SR):
    n = min(len(x), int(sr * ms / 1000))
    if n > 0:
        x[-n:] *= np.linspace(1, 0, n)
    return x


def finish(x, peak=PEAK):
    m = np.max(np.abs(x)) or 1.0
    return fade_out(x * (peak / m))


def semi(f, n):
    return f * 2 ** (n / 12.0)


# ------------------------------------------------------------------ SFX (mỗi hàm nhận biến thể v = 0,1,2…)
def sfx_UiTap(v):
    rng = np.random.default_rng(10 + v)
    t = tt(0.07)
    f = 1500 * (1 + 0.05 * v)
    y = sine(f, t) * env_exp(t, 70) * 0.8
    y += band(noise(0.07, rng), 1200, 3500) * env_exp(t, 160) * 0.5
    return y


def sfx_UiBack(v):
    rng = np.random.default_rng(20 + v)
    t = tt(0.09)
    f = semi(1500, -3) * (1 + 0.04 * v)
    y = sine(f, t) * env_exp(t, 55) * 0.8
    y += band(noise(0.09, rng), 800, 2500) * env_exp(t, 130) * 0.4
    return y


def sfx_DoorChime(v):
    root = [D5, E5, F5s][v]
    y = np.zeros(int(1.0 * SR))
    for i, f in enumerate((root, semi(root, 4), semi(root, 9))):  # tam cung ngũ cung
        place(y, bell(f, 0.7, decay=5.0, amp=0.7), 0.0 + 0.11 * i)
    return y


def sfx_OrderNew(v):
    f0 = [380, 420, 460][v]
    t = tt(0.25)
    y = glide(f0, f0 * 2.0, 0.25) * env_exp(t, 9, attack=0.01)
    y += 0.3 * sine(f0 * 4, t) * env_exp(t, 22)
    return y


def sfx_OrderSelect(v):
    rng = np.random.default_rng(30 + v)
    t = tt(0.07)
    y = sine([2300, 2500][v], t) * env_exp(t, 90) * 0.7
    y += band(noise(0.07, rng), 2000, 5000) * env_exp(t, 200) * 0.3
    return y


def sfx_CustomerTap(v):
    # "ừm?" nhỏ: glissando lên nhẹ có vibrato, thêm hài để giống giọng
    base = [210, 230, 250, 270, 190][v]
    t = tt(0.26)
    vib = 1 + 0.015 * sine(26, t)
    f0, f1 = base, base * 1.35
    k = np.log(f1 / f0) / t[-1]
    phase = 2 * np.pi * f0 * (np.exp(k * t) - 1) / k
    y = np.sin(phase * vib) + 0.5 * np.sin(2 * phase * vib) + 0.25 * np.sin(3 * phase * vib)
    return y * np.sin(np.pi * np.clip(t / t[-1], 0, 1)) ** 0.6


def sfx_StepOk(v):
    f = [D5, E5, F5s][v]
    return pluck(f, 0.35, decay=0.994, rng=np.random.default_rng(40 + v)) * env_exp(tt(0.35), 6)


def sfx_StepWrong(v):
    rng = np.random.default_rng(50 + v)
    t = tt(0.22)
    y = glide(210 - 20 * v, 120 - 10 * v, 0.22) * env_exp(t, 14)
    y += band(noise(0.22, rng), 300, 1500) * env_exp(t, 50) * 0.35
    return y


def sfx_BrewDone(v):
    notes = [(D5, F5s, A5), (E5, A5, B5), (F5s, A5, D6)][v]
    y = np.zeros(int(0.9 * SR))
    for i, f in enumerate(notes):
        t = tt(0.7)
        n = (sine(f, t) + 0.35 * sine(2 * f, t)) * env_exp(t, 5)  # kalimba / marimba
        place(y, n, 0.11 * i)
    return y


def sfx_Ruin(v):
    rng = np.random.default_rng(60 + v)
    y = np.zeros(int(0.6 * SR))
    t1 = tt(0.18)
    place(y, glide(320 - 20 * v, 70, 0.18) * env_exp(t1, 12), 0.0)
    place(y, band(noise(0.18, rng), 200, 1200) * env_exp(t1, 35) * 0.35, 0.0)
    t2 = tt(0.32)
    place(y, glide(260, 130, 0.32) * env_exp(t2, 8) * 0.7, 0.2)
    return y


def sfx_Serve(v):
    rng = np.random.default_rng(70 + v)
    t = tt(0.2)
    y = sine([220, 205, 235][v], t) * env_exp(t, 28)
    y += band(noise(0.2, rng), 400, 3000) * env_exp(t, 160) * 0.5
    return y


def sfx_CoinPay(v):
    f1 = [784, 880, 988, 1175, 1319][v]
    y = np.zeros(int(0.45 * SR))
    for at, f, a in ((0.0, f1, 1.0), (0.085, f1 * 1.335, 0.85)):
        t = tt(0.35)
        n = (sine(f, t) + 0.3 * sine(2 * f, t)) * env_exp(t, 12, attack=0.004) * a
        place(y, n, at)
    return lowpass(y, 7500)


def sfx_CustomerAngry(v):
    rng = np.random.default_rng(80 + v)
    y = np.zeros(int(0.55 * SR))
    for at, f in ((0.0, A4 if v == 0 else B4), (0.22, F4s if v == 0 else A4)):
        t = tt(0.3)
        n = sine(f, t) * np.sin(np.pi * np.clip(t / t[-1], 0, 1)) ** 0.8
        n += 0.25 * band(noise(0.3, rng), 1000, 3000) * np.sin(np.pi * np.clip(t / t[-1], 0, 1))
        place(y, n, at)
    return y


def sfx_CupPick(v):
    rng = np.random.default_rng(90 + v)
    t = tt(0.22)
    y = sine([300, 330, 280][v], t) * env_exp(t, 30)
    y += band(noise(0.22, rng), 500, 3500) * env_exp(t, 130) * 0.45
    place(y, sine(2400, tt(0.06)) * env_exp(tt(0.06), 90) * 0.3, 0.07)
    return y


def sfx_ScrapCoin(v):
    t = tt(0.13)
    f = [1480, 1660, 1320][v]
    return (sine(f, t) + 0.3 * sine(2 * f, t)) * env_exp(t, 35)


def sfx_WipeDone(v):
    rng = np.random.default_rng(100 + v)
    n = noise(0.42, rng)
    t = tt(0.42)
    swish = band(n, 1500, 5500) * np.sin(np.pi * np.clip(t / t[-1], 0, 1)) ** 2 * 0.7
    y = swish
    place(y, bell(semi(D6, v), 0.3, decay=9.0, amp=0.25), 0.28)
    return y


def sfx_BuildPlace(v):
    rng = np.random.default_rng(110 + v)
    t = tt(0.16)
    y = glide(450 - 40 * v, 140, 0.16) * env_exp(t, 22)
    y += band(noise(0.16, rng), 300, 2500) * env_exp(t, 80) * 0.35
    return y


def _drawer(rng, dur=0.18):
    t = tt(dur)
    return band(noise(dur, rng), 600, 2800) * np.sin(np.pi * np.clip(t / t[-1], 0, 1)) * 0.5


def sfx_Sell(v):
    rng = np.random.default_rng(120 + v)
    y = np.zeros(int(0.5 * SR))
    place(y, _drawer(rng), 0.0)
    place(y, bell(A4, 0.4, decay=7.0, amp=0.5), 0.14)
    return y


def sfx_Purchase(v):
    rng = np.random.default_rng(130 + v)
    y = np.zeros(int(0.6 * SR))
    place(y, _drawer(rng), 0.0)
    place(y, bell([D5, E5][v], 0.45, decay=6.0, amp=0.7), 0.15)
    return y


def sfx_LevelUp(v):
    y = np.zeros(int(1.3 * SR))
    notes = [D5, F5s, A5, D6]
    for i, f in enumerate(notes):  # gảy đàn tranh: nốt rải lên rồi ngân
        n = pluck(f, 0.9, decay=0.9985, rng=np.random.default_rng(140 + i)) * env_exp(tt(0.9), 2.2)
        place(y, n, 0.08 * i)
    place(y, bell(D6, 0.9, decay=3.0, amp=0.35), 0.32)
    return y


def sfx_Claim(v):
    y = np.zeros(int(0.9 * SR))
    count = [3, 4, 5][v]
    for i in range(count):
        f = PENTA[min(len(PENTA) - 1, 7 + (i % 4))] * (1.0 - 0.01 * i)
        t = tt(0.3)
        n = (sine(f, t) + 0.3 * sine(2 * f, t)) * env_exp(t, 14) * (1.0 - 0.1 * i)
        place(y, n, 0.07 * i)
    return lowpass(y, 8000)


def sfx_Chest(v):
    y = sfx_Claim(2)
    y = np.concatenate([y, np.zeros(int(0.3 * SR))])
    place(y, bell(D5, 0.8, decay=4.0, amp=0.6), 0.1)
    return y


def sfx_Fanfare(v):
    y = np.zeros(int(3.0 * SR))
    for i, f in enumerate([D5, F5s, A5, D6, F6s]):
        n = (bell(f, 1.2, decay=3.2, amp=0.8)
             + 0.6 * pluck(f, 1.2, decay=0.998, rng=np.random.default_rng(150 + i)) * env_exp(tt(1.2), 1.6))
        place(y, n, 0.14 * i)
    for f in (D5, A5, F6s):  # hợp âm ngân
        t = tt(2.4)
        place(y, 0.22 * sine(f, t) * env_exp(t, 1.4, attack=0.05), 0.55)
    return y


def sfx_DayOpen(v):
    y = np.zeros(int(1.2 * SR))
    for at in (0.0, 0.16):  # chuông xe đạp: hai tiếng "kring"
        place(y, bell(2050, 0.55, decay=7.0, amp=0.55), at)
    t = tt(0.8)
    flute = sine(A5, t) * (1 + 0.012 * sine(5.5, t)) * np.sin(np.pi * np.clip(t / t[-1], 0, 1)) ** 0.7
    place(y, 0.35 * flute, 0.4)
    return y


def sfx_DayClose(v):
    rng = np.random.default_rng(160)
    y = np.zeros(int(1.2 * SR))
    for i, f in enumerate([A5, F5s, D5]):
        place(y, bell(f, 0.7, decay=5.0, amp=0.5), 0.2 * i)
    click = band(noise(0.06, rng), 400, 3000) * env_exp(tt(0.06), 90)
    place(y, click * 0.8, 0.9)
    return y


# Id -> (hàm sinh, số biến thể). Phải khớp enum Sfx trong Scripts/Audio/Sfx.cs.
SFX = {
    "UiTap": (sfx_UiTap, 3), "UiBack": (sfx_UiBack, 3), "DoorChime": (sfx_DoorChime, 3), "OrderNew": (sfx_OrderNew, 3),
    "OrderSelect": (sfx_OrderSelect, 2), "CustomerTap": (sfx_CustomerTap, 5), "StepOk": (sfx_StepOk, 3),
    "StepWrong": (sfx_StepWrong, 2), "BrewDone": (sfx_BrewDone, 3), "Ruin": (sfx_Ruin, 2), "Serve": (sfx_Serve, 3),
    "CoinPay": (sfx_CoinPay, 5), "CustomerAngry": (sfx_CustomerAngry, 2), "CupPick": (sfx_CupPick, 3),
    "ScrapCoin": (sfx_ScrapCoin, 3), "WipeDone": (sfx_WipeDone, 3), "BuildPlace": (sfx_BuildPlace, 2),
    "Sell": (sfx_Sell, 1), "Purchase": (sfx_Purchase, 2), "LevelUp": (sfx_LevelUp, 1), "Claim": (sfx_Claim, 3),
    "Chest": (sfx_Chest, 1), "Fanfare": (sfx_Fanfare, 1), "DayOpen": (sfx_DayOpen, 1), "DayClose": (sfx_DayClose, 1),
}


def write_wav(path, x, sr=SR):
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.clip(x, -1, 1)
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1 if pcm.ndim == 1 else pcm.shape[1])
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes((pcm * 32767).astype("<i2").tobytes())


# ------------------------------------------------------------------ nhạc nền (placeholder)
def render_music(name, bpm, bars, chords, density, seed, pad_amp, pluck_amp, octave_shift=0):
    """Vòng nhạc liền mạch: pad + bass + giai điệu ngũ cung gảy thưa. Phần đuôi tràn qua cuối vòng được gập lại về đầu."""
    sr = MUSIC_SR
    rng = np.random.default_rng(seed)
    beat = 60.0 / bpm
    bar = 4 * beat
    length = int(bars * bar * sr)
    tail = int(4.0 * sr)
    L = np.zeros(length + tail)
    R = np.zeros(length + tail)

    def add(sig, at, pan):
        i = int(at * sr)
        n = min(len(sig), len(L) - i)
        if n <= 0:
            return
        L[i:i + n] += sig[:n] * (1 - pan) * 1.2
        R[i:i + n] += sig[:n] * pan * 1.2

    for b in range(bars):
        root, third, fifth = chords[b % len(chords)]
        at = b * bar
        t = tt(bar + 1.5, sr)
        swell = np.minimum(1.0, t / 0.8) * np.exp(-np.maximum(0, t - bar) * 2.0)
        for f, pan in ((root, 0.35), (third, 0.5), (fifth, 0.65)):
            pad = (sine(f, t) + 0.5 * sine(2 * f, t, 0.7) + 0.2 * sine(3 * f, t)) * swell * pad_amp * 0.33
            add(pad, at, pan)
        bass = sine(root / 2, tt(2 * beat, sr)) * env_exp(tt(2 * beat, sr), 1.6, attack=0.01) * pad_amp * 1.2
        add(bass, at, 0.5)
        # giai điệu: bước ngẫu nhiên trên gam ngũ cung, mỗi phách có xác suất `density`
        idx = int(rng.integers(3, 8))
        for k in range(8):  # 8 nốt móc đơn trong ô nhịp
            if rng.random() > density:
                continue
            idx = int(np.clip(idx + rng.integers(-2, 3), 0, len(PENTA) - 1))
            f = PENTA[idx] * (2 ** octave_shift)
            n = pluck(f, 1.6, sr=sr, decay=0.9975, rng=np.random.default_rng(int(rng.integers(0, 1 << 30)))) \
                * env_exp(tt(1.6, sr), 1.3) * pluck_amp
            add(n, at + k * beat / 2, float(rng.uniform(0.3, 0.7)))

    for ch in (L, R):  # gập đuôi về đầu để vòng lặp không bị cắt
        ch[:tail] += 0
        ch[:len(ch) - length] += ch[length:]
    L, R = L[:length], R[:length]
    out = np.stack([L, R], axis=1)
    out *= 0.6 / (np.max(np.abs(out)) or 1.0)
    return out, sr


# Hợp âm theo gam Rê trưởng: I - vi - IV - V (bậc gốc / 3 / 5)
CH_DAY = [(146.83, 185.0, 220.0), (123.47, 146.83, 185.0), (196.0, 246.94, 293.66), (220.0, 277.18, 329.63)]


def encode_ogg(wav_path, ogg_path):
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav_path), "-c:a", "vorbis", "-strict", "-2", "-q:a", "4", str(ogg_path)]
    subprocess.run(cmd, check=True)


MUSIC = {
    "Title": dict(bpm=66, bars=8, chords=CH_DAY, density=0.55, seed=1, pad_amp=0.9, pluck_amp=1.0),
    "Day": dict(bpm=84, bars=8, chords=CH_DAY, density=0.42, seed=2, pad_amp=0.8, pluck_amp=0.9),
    "Calm": dict(bpm=58, bars=8, chords=CH_DAY, density=0.22, seed=3, pad_amp=1.0, pluck_amp=0.7),
}


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", nargs="*", help="chỉ sinh các id này (Sfx hoặc Bgm), vd UiTap Day")
    ap.add_argument("--no-music", action="store_true")
    args = ap.parse_args(argv)
    only = set(args.only or [])

    for name, (fn, variants) in SFX.items():
        if only and name not in only:
            continue
        for v in range(variants):
            suffix = "" if variants == 1 else "_%d" % (v + 1)
            write_wav(SFX_DIR / ("AU_Sfx_%s%s.wav" % (name, suffix)), finish(np.asarray(fn(v), dtype=np.float64)))
        print("SFX", name, "x%d" % variants)

    if not args.no_music:
        for name, kw in MUSIC.items():
            if only and name not in only:
                continue
            x, sr = render_music(name, **kw)
            BGM_DIR.mkdir(parents=True, exist_ok=True)
            tmp = BGM_DIR / ("_tmp_%s.wav" % name)
            write_wav(tmp, x, sr)
            try:
                encode_ogg(tmp, BGM_DIR / ("AU_Bgm_%s.ogg" % name))
            finally:
                tmp.unlink(missing_ok=True)
            print("BGM", name)


if __name__ == "__main__":
    main(sys.argv[1:])
