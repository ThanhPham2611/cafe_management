# Âm thanh: nguồn và giấy phép

## Hiện tại: toàn bộ là placeholder tự sinh
- `game/Assets/_Project/Art/Audio/Sfx/AU_Sfx_*.wav` (61 file, 25 id) và `Bgm/AU_Bgm_{Title,Day,Calm}.ogg` do `tools/gen_audio.py` tổng hợp bằng numpy (sin, chuông phi hoà, Karplus-Strong, nhiễu lọc). Không có mẫu âm thanh của bên thứ ba, không vướng bản quyền; chất lượng chỉ để nghe thử. Chạy lại: `python3 tools/gen_audio.py` (cần python3 + numpy + ffmpeg có encoder `vorbis`).
- Âm "hữu cơ" (rót, khuấy, lau, đặt ly) và nhạc nền nên được thay trước khi phát hành.

## Cách thay bằng file thật
1. Bỏ file (WAV hoặc OGG) vào `Art/Audio/Sfx` đặt tên `AU_Sfx_<Id>[_n].wav` (`<Id>` đúng tên enum `Sfx` trong `Scripts/Audio/Sfx.cs`; `_n` = số biến thể 1, 2, 3...) hoặc `Art/Audio/Bgm/AU_Bgm_<Id>.ogg` (`Title`, `Day`, `Calm`).
2. Xoá file placeholder cùng id nếu không muốn trộn.
3. Unity: menu `Cafe/Audio/Ensure Audio Library` (không ghi đè âm lượng / cooldown đã chỉnh tay), rồi commit `Resources/Audio/AudioLibrary.asset`.
4. Nghe thử từng id: vào Play Mode, menu `Cafe/Audio/Test Sfx (Play Mode)`.
5. Ghi nguồn + giấy phép từng file thật vào bảng dưới.

## Gợi ý nguồn (kiểm tra lại giấy phép trên trang tải trước khi dùng)
| Nguồn | Giấy phép |
|---|---|
| Kenney: Interface Sounds, UI Audio, Impact Sounds, Casino Audio, RPG Audio, Music Jingles (kenney.nl) | CC0 |
| Freesound.org | từng file; chỉ lấy CC0 (CC-BY thì phải ghi công) |
| OpenGameArt.org | từng file; lọc CC0 |
| Sonniss GameAudioGDC | giấy phép riêng, dùng thương mại được |
| Pixabay (sound effects, music) | Pixabay Content License (không phải CC0) |
| Incompetech (Kevin MacLeod) | CC-BY 4.0, cần màn hình ghi công, nên tránh |

Mẹo: tự thu bằng điện thoại khoảng một giờ (ly thuỷ tinh trên gỗ, ghế nhựa, thìa trong ly, giọt phin, khăn lau bàn, chuông xe đạp) cho game chất riêng và không phải theo dõi giấy phép.

## File thật đã dùng
| File | Nguồn | Giấy phép |
|---|---|---|
| (chưa có) | | |
