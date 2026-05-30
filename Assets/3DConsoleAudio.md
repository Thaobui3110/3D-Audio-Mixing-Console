# Spatial Audio Sandbox — Tổng hợp Project

*Phiên bản cuối cùng — tháng 5/2026*

---

## 1. Tổng quan

Project khởi đầu là **3D Piano Instrument Demo** (tương tác phím piano bằng FPS controller + ChucK audio engine). Qua nhiều lần refactor, đã được redesign hoàn toàn thành **Spatial Audio Processor Sandbox** — một môi trường 3D cho phép user upload file nhạc, phát từ các speaker 3D trong không gian, di chuyển FPS để nghe spatial audio thay đổi realtime, và điều chỉnh EQ / Reverb / Compressor qua UI panel.

Các tính năng chính của phiên bản cuối:

- Upload audio (.wav / .mp3 / .ogg) lúc runtime qua Browse file dialog hoặc nhập path
- Speaker 3D tự tạo (Cube runtime phát sáng) hoặc từ prefab, spawn trước mặt player
- Di chuyển FPS quanh speaker, nghe spatial audio thay đổi theo khoảng cách và hướng
- Occlusion: đứng sau tường → âm thanh tự động bị low-pass filter
- Điều chỉnh Low Pass, High Pass, Mid EQ, Reverb, Compressor, Master Volume realtime
- Spectrum FFT + Waveform visualization
- Speaker mesh phản ứng trực quan với âm thanh (scale theo bass, phát sáng theo volume)
- Không còn phụ thuộc Chunity / ChucK

---

## 2. Kiến trúc cuối cùng

### 2.1 Hierarchy

```
SampleScene
│
├── Systems
│   ├── AudioManager                      [GameObject]
│   │   ├── AudioProcessorController      ← DSP bridge → AudioMixer
│   │   ├── SpatialAudioManager           ← spawn/manage speakers
│   │   ├── AudioFileLoader               ← load WAV/MP3/OGG runtime
│   │   └── SpectrumAnalyzer              ← FFT + waveform data
│   │
│   └── EventSystem                       [Unity default]
│
├── Player                                [CharacterController + FPSController]
│   └── Main Camera                       [Camera + AudioListener]
│
├── AudioWorld                            [empty parent Transform]
│   ├── Speaker_01                        [SpatialSoundObject + AudioReactiveObject]
│   ├── Speaker_02                        (hoặc Cube runtime tự tạo khi chưa có prefab)
│   └── ...
│
├── Environment
│   ├── Floor
│   ├── Walls                             [Layer = "Wall" — dùng cho occlusion]
│   └── Directional Light
│
└── Canvas                                [Screen Space Overlay, 1920×1080]
    └── AudioProcessorPanel               [AudioProcessorUI]
        ├── UploadSection (Browse + Load)
        ├── TransportSection (Play / Stop / Spawn + Dropdown)
        ├── EQ Sliders (LowPass, HighPass, MidEQ)
        ├── Reverb Sliders (Wet Level, Decay Time)
        ├── Compressor Sliders (Threshold, Make Up Gain)
        ├── Master Slider
        └── StatusLabel
```

### 2.2 Scripts

```
Assets/Scripts/
├── Audio/
│   ├── AudioProcessorController.cs    ← Bridge UI → AudioMixer (Set/Get DSP params)
│   ├── SpatialSoundObject.cs          ← Component speaker 3D + occlusion tích hợp
│   ├── SpatialAudioManager.cs         ← Singleton spawn/manage/load speakers
│   ├── AudioFileLoader.cs             ← Load WAV/MP3/OGG runtime qua UnityWebRequest
│   ├── SpectrumAnalyzer.cs            ← FFT + waveform từ AudioListener native
│   └── AudioReactiveObject.cs         ← Speaker mesh scale/glow theo audio
│
├── Player/
│   └── FPSController.cs               ← Di chuyển FPS + Tab = UI Mode
│
├── UI/
│   └── AudioProcessorUI.cs            ← Quản lý toàn bộ UI panel
│
├── Visualization/
│   ├── Spectrum.cs                    ← Hiển thị FFT spectrum (N khối 3D)
│   └── Waveform.cs                    ← Hiển thị waveform (1024 khối 3D)
│
└── Editor/
    └── AudioProcessorUIBuilder.cs     ← Editor tool tự tạo Canvas + Panel
```

---

## 3. Flow hoạt động chính

### 3.1 Upload và phát audio

```
User: Play scene
  → Nhấn Tab (UI Mode — cursor free)
  → Nhấn Browse → OS file dialog mở → chọn file .wav/.mp3/.ogg
  → Path tự điền vào InputField
  → Nhấn Load
     │
     ├── Chưa chọn speaker trong dropdown:
     │   └── LoadAndSpawn(path)
     │       ├── SpawnSpeaker() → tạo Cube runtime trước mặt player (4-6m)
     │       │   ├── Nếu có prefab → Instantiate(prefab)
     │       │   └── Nếu không → CreateRuntimeSpeaker() → Cube 0.6m + URP/Lit + Emission
     │       │
     │       └── AudioFileLoader.LoadFile(path) → UnityWebRequest coroutine
     │           └── OnClipLoaded → HandleClipLoaded
     │               └── speaker.SetClip(clip) + speaker.Play()
     │
     └── Đã chọn speaker trong dropdown:
         └── LoadAndAssign(path, speakerID)
             └── Load file → assign clip vào speaker đã có → Play
```

### 3.2 Điều chỉnh DSP

```
User: Tab → UI Mode → kéo slider
  → AudioProcessorUI.OnXxxChanged(value)
     → AudioProcessorController.SetXxx(value)
        → AudioMixer.SetFloat(parameterName, convertedValue)
           → Audio output thay đổi realtime
```

### 3.3 Spatial audio + Occlusion

```
User di chuyển (WASD) quanh speaker trong không gian 3D
  → AudioListener trên Camera nhận audio spatial (volume, pan thay đổi theo khoảng cách/hướng)
  → SpatialSoundObject.Update() raycast đến AudioListener
     → Nếu tia chạm Wall layer → AudioLowPassFilter.cutoffFrequency giảm (âm bị bịt)
     → Nếu không bị chắn → cutoff trở về 22000 Hz (nghe rõ)
```

---

## 4. Bugs lớn gặp phải

Dưới đây là tổng hợp tất cả bugs nghiêm trọng phát hiện và fix trong quá trình phát triển, sắp xếp theo mức độ ảnh hưởng.

### BUG 1 — LoadAndSpawn: sourceID lệch → clip không bao giờ được assign

**Mức độ: Nghiêm trọng** — toàn bộ flow upload audio bị đứt hoàn toàn.

**Triệu chứng:** Nhấn Load → speaker spawn nhưng hoàn toàn im lặng, không phát nhạc.

**Nguyên nhân gốc:** Timing giữa `Instantiate()` và `Awake()` trong Unity.

```
Instantiate(speakerPrefab)
  └── Awake() chạy NGAY tại đây
      └── sourceID = gameObject.name → "Speaker(Clone)"

go.name = "Speaker_01"     ← đổi tên GO, nhưng sourceID VẪN = "Speaker(Clone)"
sources["Speaker_01"] = obj ← dict key = "Speaker_01"

pendingAssignID = obj.SourceID → "Speaker(Clone)"  ← LỆCH!

HandleClipLoaded:
  sources.TryGetValue("Speaker(Clone)") → FALSE → clip bị bỏ qua
```

**Fix:**
- `SpatialSoundObject`: thêm method `SetSourceID(string id)`
- `SpatialAudioManager.SpawnSpeaker()`: gọi `obj.SetSourceID(id)` ngay sau `go.name = id`

**File ảnh hưởng:** `SpatialSoundObject.cs`, `SpatialAudioManager.cs`

---

### BUG 2 — TMP_InputField thiếu textViewport → không nhập được text → Load vô nghĩa

**Mức độ: Nghiêm trọng** — user không thể nhập đường dẫn file.

**Triệu chứng:** InputField nhìn bình thường nhưng click vào không có caret, gõ không hiện text. Nhấn Load luôn báo "Nhập đường dẫn file trước khi load."

**Nguyên nhân gốc:** `AudioProcessorUIBuilder.MakeUploadSection()` tạo `TMP_InputField` nhưng không set `textViewport`. TMP_InputField yêu cầu một RectTransform có `RectMask2D` làm viewport để caret rendering và text clipping hoạt động.

```csharp
// Code lỗi — thiếu textViewport
var inputField = inputGO.AddComponent();
inputField.textComponent = textTMP;     // ✓ có
inputField.placeholder   = phTMP;       // ✓ có
inputField.textViewport  = ???;         // ✗ THIẾU
```

**Fix:** Thêm "Text Area" GameObject với `RectMask2D`, set làm `inputField.textViewport`. Text và Placeholder trở thành con của Text Area thay vì con trực tiếp của InputGO.

**File ảnh hưởng:** `AudioProcessorUIBuilder.cs`

---

### BUG 3 — Slider click được nhưng không drag được

**Mức độ: Cao** — tất cả 8 DSP sliders không điều khiển được.

**Triệu chứng:** Nhấn Tab vào UI Mode, click slider thấy phản hồi nhưng kéo handle không di chuyển.

**Nguyên nhân gốc:** `HorizontalLayoutGroup` trên slider row set `childControlWidth = false`. Slider GO có `LayoutElement.flexibleWidth = 1` nhưng bị layout group bỏ qua hoàn toàn → slider nhận width = 0 từ default RectTransform → handle 20×20px vẫn hiển thị nhưng không có vùng slide area.

```csharp
// Code lỗi
hlg.childControlWidth = false;  // ← LayoutElement bị bỏ qua

// Slider GO
sliderGO.AddComponent().flexibleWidth = 1f; // ← vô hiệu!
```

**Fix:** `childControlWidth = true` cho tất cả `HorizontalLayoutGroup` rows (slider, upload, transport).

**File ảnh hưởng:** `AudioProcessorUIBuilder.cs`

---

### BUG 4 — Speaker có sẵn trong scene không được đăng ký

**Mức độ: Trung bình** — dropdown trống, Play/Stop/Pause không ảnh hưởng speaker đã đặt sẵn.

**Triệu chứng:** Speaker_01/02/03 đặt sẵn trong Hierarchy không xuất hiện trong dropdown, không phản hồi Play/Stop từ UI.

**Nguyên nhân gốc:** `SpatialAudioManager` chỉ đăng ký speaker qua `SpawnSpeaker()`. Các speaker đặt sẵn trong scene không được quét và thêm vào `sources` dictionary.

**Fix:** Thêm `RegisterExistingSpeakers()` trong `Start()` — duyệt `audioWorldParent.GetComponentsInChildren<SpatialSoundObject>()`, đăng ký vào dict, cập nhật `spawnCounter` để tránh trùng ID khi spawn mới.

**File ảnh hưởng:** `SpatialAudioManager.cs`

---

### BUG 5 — Field name mismatch: Compressor Make Up Gain

**Mức độ: Trung bình** — slider Make Up Gain không link vào UI component.

**Triệu chứng:** Slider Make Up Gain hiển thị nhưng kéo không có tác dụng (giá trị không thay đổi trong mixer).

**Nguyên nhân gốc:** Tên field trong `AudioProcessorUIBuilder.SliderConfig` không khớp với tên field serialized trong `AudioProcessorUI`.

```
UIBuilder:        sliderCompMakeUp     / labelCompMakeUp
AudioProcessorUI: sliderCompMakeupGain / labelCompMakeupGain
                  ↑ LỆCH
```

`SafeSet()` tìm `sliderCompMakeUp` trong SerializedObject → không tìm thấy → log warning → slider không link.

**Fix:** Sửa field names trong `SliderConfig` thành `sliderCompMakeupGain` / `labelCompMakeupGain`.

**File ảnh hưởng:** `AudioProcessorUIBuilder.cs`

---

### BUG 6 — MakeButton tạo LayoutElement trùng lặp

**Mức độ: Thấp** — button vẫn hoạt động nhưng có component thừa.

**Nguyên nhân gốc:** `SetH()` đã tạo `LayoutElement` (get-or-add), sau đó `MakeButton` gọi `AddComponent<LayoutElement>()` → 2 LayoutElement trên cùng 1 GameObject.

**Fix:** `GetComponent<LayoutElement>()` thay vì `AddComponent` trong `MakeButton`.

**File ảnh hưởng:** `AudioProcessorUIBuilder.cs`

---

### BUG 7 — Speaker spawn ở gốc tọa độ, không nhìn thấy

**Mức độ: Trung bình** — user không biết speaker ở đâu.

**Triệu chứng:** Nhấn Load hoặc Spawn → status báo thành công nhưng không thấy speaker trong 3D.

**Nguyên nhân gốc:** `RandomSpawnPosition()` tạo vị trí ngẫu nhiên quanh gốc tọa độ (0,0,0) chứ không phải quanh player. Nếu player đã di chuyển xa, speaker xuất hiện ở vị trí không nhìn thấy. Thêm vào đó, nếu chưa gán `speakerPrefab` thì hàm return null và không tạo gì.

**Fix:**
- `SpawnInFrontOfPlayer()`: tính vị trí 4-6m trước mặt camera, lệch ngang nhẹ để tránh chồng
- `CreateRuntimeSpeaker()`: nếu không có prefab → tạo Cube 0.6m runtime với material URP/Lit phát sáng + Emission, mỗi speaker một màu khác nhau
- Status bar hiển thị tên + tọa độ spawn

**File ảnh hưởng:** `SpatialAudioManager.cs`, `AudioProcessorUI.cs`

---

### Bugs từ các phiên bản trước (đã fix trước session này)

| Bug | Phiên bản | Mô tả ngắn |
|---|---|---|
| UIBuilder NullReferenceException | v5 | `FindProperty().objectReferenceValue` không check null, handle sizeDelta (18,0) vô hình |
| UI Mode slider không kéo (CharacterController) | v6 | CharacterController intercept mouse drag events → disable khi UI Mode |
| FPSController gravity không tích lũy | v1 | Vận tốc rơi cần tích lũy mỗi frame |
| AudioProcessorController duplicate SetLowPass | v1 | 2 method trùng tên gây conflict |
| InstrumentInteractor raycast sai góc | v1 | Phải đặt trên Camera child, không phải Player root |
| Unity ParamEQ không có Shelf | v3 | Chuyển sang Lowpass + Highpass + ParamEQ Gain |
| Unity Compressor không có Ratio | v4 | Chuyển sang Make Up Gain thay Ratio |

---

## 5. Danh sách file cuối cùng đã sửa

| File | Thay đổi chính |
|---|---|
| `SpatialAudioManager.cs` | SpawnInFrontOfPlayer, CreateRuntimeSpeaker (Cube), RegisterExistingSpeakers, SetSourceID sync, fix RandomSpawnPosition reference |
| `SpatialSoundObject.cs` | Thêm SetSourceID() method |
| `AudioProcessorUI.cs` | Thêm browseButton + OnBrowseClicked (EditorUtility.OpenFilePanel), OnSpawnClicked hiển thị vị trí |
| `AudioProcessorUIBuilder.cs` | childControlWidth = true, TMP_InputField textViewport + RectMask2D, field name fix, Browse button, MakeButton LayoutElement fix |
| `FPSController.cs` | Tab UI Mode disable CharacterController (fix từ v6, không sửa thêm) |
| `AudioProcessorController.cs` | Không sửa trong session này |
| `AudioFileLoader.cs` | Không sửa trong session này |

---

## 6. Hướng dẫn sử dụng (phiên bản cuối)

### Setup nhanh

1. Copy tất cả scripts vào đúng thư mục trong `Assets/Scripts/`
2. Đảm bảo có AudioWorld GameObject (empty) trong scene
3. Setup AudioManager với `SpatialAudioManager` — chỉ cần gán `audioWorldParent` (prefab tuỳ chọn)
4. Setup AudioMixer: PianoMixer → Piano group → Highpass, Lowpass, ParamEQ, SFX Reverb, Compressor (expose 8 parameters)
5. Menu: **Tools → Build Audio Processor UI** → nhấn Build
6. Kéo AudioManager vào fields Processor / Audio Manager / File Loader trên AudioProcessorPanel

### Test

1. Play scene
2. Nhấn **Tab** → UI Mode (cursor free)
3. Nhấn **Browse** → chọn file `.wav / .mp3 / .ogg`
4. Nhấn **Load** → Cube speaker phát sáng xuất hiện trước mặt, nhạc phát
5. Nhấn **Tab** lại → di chuyển WASD, nghe spatial audio thay đổi
6. Nhấn **Tab** → kéo sliders chỉnh EQ / Reverb / Compressor
7. Đi sau tường → nghe occlusion low-pass tự kích hoạt

### Keybinds

| Key | Chức năng |
|---|---|
| WASD / Arrows | Di chuyển |
| Shift | Chạy nhanh |
| Mouse | Nhìn (FPS) |
| Escape | Toggle cursor lock |
| **Tab** | Toggle UI Mode — cursor free để dùng sliders và buttons |

### Slider ranges

| Slider | Range | Default | Unit |
|---|---|---|---|
| Low Pass | 200 – 22000 | 22000 | Hz |
| High Pass | 20 – 2000 | 20 | Hz |
| Mid EQ | -12 – +12 | 0 | dB |
| Reverb Wet | 0 – 1 | 0 | % |
| Reverb Decay | 0.1 – 10 | 1 | s |
| Comp Threshold | -60 – 0 | 0 | dB |
| Comp Make Up Gain | 0 – 20 | 0 | dB |
| Master Volume | 0 – 1 | 1 | % |

---

## 7. AudioMixer Setup

Mixer: **PianoMixer** → Group: **Piano**

Effects trên Piano group (theo thứ tự):

```
Highpass
Lowpass
ParamEQ         ← Center Freq = 1000 Hz, Bandwidth = 1.0
SFX Reverb
Compressor
```

Exposed parameters:

| Effect | Parameter | Tên expose |
|---|---|---|
| Highpass | Cutoff freq | `HighPassCutoff` |
| Lowpass | Cutoff freq | `LowPassCutoff` |
| ParamEQ | Gain | `MidEQGain` |
| SFX Reverb | Room | `ReverbWet` |
| SFX Reverb | Decay Time | `ReverbDecayTime` |
| Compressor | Threshold | `CompThreshold` |
| Compressor | Make Up Gain | `CompMakeUpGain` |
| Piano (Attenuation) | Volume | `MasterVolume` |

---

## 8. Lịch sử phát triển

| Version | Nội dung chính |
|---|---|
| v1 | Refactor Piano Project — gộp scripts, fix gravity, tạo Editor tools |
| v2 | Redesign hoàn toàn thành Spatial Audio Sandbox — xóa Piano/ChucK, thêm SpatialSoundObject + AudioFileLoader + SpectrumAnalyzer |
| v3 | Chuyển EQ sang Lowpass + Highpass + ParamEQ (Unity không có Shelf) |
| v4 | Compressor Ratio → Make Up Gain (Unity không có Ratio) |
| v5 | Fix UIBuilder NullReferenceException — viết lại hoàn toàn |
| v6 | Fix UI Mode slider — disable CharacterController khi Tab |
| v7 | Fix 7 bugs lớn + thêm Browse button + Cube runtime speaker + spawn trước mặt player |