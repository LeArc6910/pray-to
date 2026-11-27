# A small client for a local ComfyUI (Docs/ArtSources.md 로컬 AI로 보스 그림, 2026-11-28): one picture from text
# (Z-Image-Turbo with a pixel-art LoRA) and the same picture turned to another angle (Qwen-Image-Edit-2511 GGUF with
# the Multiple-Angles LoRA and the 4-step Lightning LoRA). The models must be in ComfyUI's model folders and the
# ComfyUI-GGUF nodes installed; ComfyUI listening on COMFY (default http://127.0.0.1:8190). Pictures go to out/.
#   python -I comfy.py t2i <out_prefix> <prompt file> <seed> [<seed> ...]     (LORA=1.0 for the pixel-art LoRA's weight)
#   python -I comfy.py angle <input.png> <out_prefix> <seed> "<sks> left side view elevated shot medium shot"
import json, os, sys, time, urllib.request, urllib.parse, uuid

HOST = os.environ.get("COMFY", "http://127.0.0.1:8190")
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
os.makedirs(OUT, exist_ok=True)


def call(path, data=None):
    req = urllib.request.Request(HOST + path, data=json.dumps(data).encode() if data is not None else None,
                                 headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=600) as r:
            return json.loads(r.read())
    except urllib.error.HTTPError as e:
        print(e.read().decode()[:3000])
        raise


def run(graph, prefix):
    pid = call("/prompt", {"prompt": graph, "client_id": str(uuid.uuid4())})["prompt_id"]
    while True:
        h = call("/history/" + pid)
        if pid in h:
            st = h[pid].get("status", {})
            if st.get("status_str") == "error":
                print(json.dumps(st, ensure_ascii=False)[:3000])
                sys.exit(1)
            if st.get("completed"):
                break
        time.sleep(2)
    saved = []
    for node in h[pid]["outputs"].values():
        for i, img in enumerate(node.get("images", [])):
            q = urllib.parse.urlencode({"filename": img["filename"], "subfolder": img["subfolder"], "type": img["type"]})
            with urllib.request.urlopen(HOST + "/view?" + q) as r:
                dst = os.path.join(OUT, f"{prefix}.png" if i == 0 else f"{prefix}_{i}.png")
                open(dst, "wb").write(r.read())
                saved.append(dst)
    return saved


def t2i(prompt, seed, lora=0.8, w=1024, h=1024, steps=8):
    return {
        "1": {"class_type": "UNETLoader", "inputs": {"unet_name": "z_image_turbo_int8_convrot.safetensors", "weight_dtype": "default"}},
        "2": {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen_3_4b_fp8_mixed.safetensors", "type": "lumina2", "device": "default"}},
        "3": {"class_type": "VAELoader", "inputs": {"vae_name": "ae.safetensors"}},
        "4": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["1", 0], "lora_name": "pixel_art_style_z_image_turbo.safetensors", "strength_model": lora}},
        "5": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["4", 0], "shift": 3.0}},
        "6": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["2", 0], "text": prompt}},
        "7": {"class_type": "ConditioningZeroOut", "inputs": {"conditioning": ["6", 0]}},
        "8": {"class_type": "EmptySD3LatentImage", "inputs": {"width": w, "height": h, "batch_size": 1}},
        "9": {"class_type": "KSampler", "inputs": {"model": ["5", 0], "positive": ["6", 0], "negative": ["7", 0], "latent_image": ["8", 0],
                                                    "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "res_multistep", "scheduler": "simple", "denoise": 1.0}},
        "10": {"class_type": "VAEDecode", "inputs": {"samples": ["9", 0], "vae": ["3", 0]}},
        "11": {"class_type": "SaveImage", "inputs": {"images": ["10", 0], "filename_prefix": "serpent_t2i"}},
    }


def angle(image_name, prompt, seed, lora=0.9, steps=4):
    return {
        "1": {"class_type": "UnetLoaderGGUF", "inputs": {"unet_name": "qwen-image-edit-2511-Q4_K_M.gguf"}},
        "2": {"class_type": "CLIPLoader", "inputs": {"clip_name": "qwen_2.5_vl_7b_fp8_scaled.safetensors", "type": "qwen_image", "device": "default"}},
        "3": {"class_type": "VAELoader", "inputs": {"vae_name": "qwen_image_vae.safetensors"}},
        "4": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["1", 0], "lora_name": "Qwen-Image-Edit-2511-Lightning-4steps-V1.0-bf16.safetensors", "strength_model": 1.0}},
        "5": {"class_type": "LoraLoaderModelOnly", "inputs": {"model": ["4", 0], "lora_name": "qwen-image-edit-2511-multiple-angles-lora.safetensors", "strength_model": lora}},
        "6": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["5", 0], "shift": 3.1}},
        "7": {"class_type": "CFGNorm", "inputs": {"model": ["6", 0], "strength": 1.0}},
        "8": {"class_type": "LoadImage", "inputs": {"image": image_name}},
        "9": {"class_type": "ImageScaleToTotalPixels", "inputs": {"image": ["8", 0], "upscale_method": "nearest-exact", "megapixels": 1.0, "resolution_steps": 16}},
        "10": {"class_type": "TextEncodeQwenImageEditPlus", "inputs": {"clip": ["2", 0], "prompt": prompt, "vae": ["3", 0], "image1": ["9", 0]}},
        "11": {"class_type": "TextEncodeQwenImageEditPlus", "inputs": {"clip": ["2", 0], "prompt": "", "vae": ["3", 0], "image1": ["9", 0]}},
        "12": {"class_type": "FluxKontextMultiReferenceLatentMethod", "inputs": {"conditioning": ["10", 0], "reference_latents_method": "index_timestep_zero"}},
        "13": {"class_type": "FluxKontextMultiReferenceLatentMethod", "inputs": {"conditioning": ["11", 0], "reference_latents_method": "index_timestep_zero"}},
        "14": {"class_type": "VAEEncode", "inputs": {"pixels": ["9", 0], "vae": ["3", 0]}},
        "15": {"class_type": "KSampler", "inputs": {"model": ["7", 0], "positive": ["12", 0], "negative": ["13", 0], "latent_image": ["14", 0],
                                                     "seed": seed, "steps": steps, "cfg": 1.0, "sampler_name": "euler", "scheduler": "simple", "denoise": 1.0}},
        "16": {"class_type": "VAEDecode", "inputs": {"samples": ["15", 0], "vae": ["3", 0]}},
        "17": {"class_type": "SaveImage", "inputs": {"images": ["16", 0], "filename_prefix": "serpent_angle"}},
    }


def upload(path):
    boundary = uuid.uuid4().hex
    name = os.path.basename(path)
    body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{name}\"\r\n"
            f"Content-Type: image/png\r\n\r\n").encode() + open(path, "rb").read() + \
           f"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n--{boundary}--\r\n".encode()
    req = urllib.request.Request(HOST + "/upload/image", data=body, headers={"Content-Type": f"multipart/form-data; boundary={boundary}"})
    with urllib.request.urlopen(req) as r:
        return json.loads(r.read())["name"]


if __name__ == "__main__":
    mode = sys.argv[1]
    if mode == "t2i":
        prompt = open(sys.argv[3] if len(sys.argv) > 3 and not sys.argv[3].isdigit() else os.path.join(HERE, "prompt.txt"), encoding="utf-8").read().strip()
        seeds = [int(s) for s in sys.argv[3:] if s.isdigit()]
        lora = float(os.environ.get("LORA", "0.8"))
        for s in seeds:
            t = time.time()
            print(run(t2i(prompt, s, lora), f"{sys.argv[2]}_{s}"), f"{time.time() - t:.1f}s", flush=True)
    elif mode == "angle":
        name = upload(sys.argv[2])
        t = time.time()
        print(run(angle(name, sys.argv[5], int(sys.argv[4]), float(os.environ.get("LORA", "0.9"))), sys.argv[3]), f"{time.time() - t:.1f}s", flush=True)
