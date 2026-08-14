"""用 OpenRouter 免费视觉模型审查模拟图。

用法：.venv/bin/python vision_review.py <image.png> [model]
依赖环境变量 OPENROUTER_API_KEY。
"""
import base64
import json
import os
import sys
import urllib.request

API = "https://openrouter.ai/api/v1/chat/completions"
PROMPT = """这是一个环世界(RimWorld)风格 MOD 管理弹窗的 UI 模拟图（1000x720 逻辑像素）。
请扮演严格的 UI 审查员，逐条检查并报告（指出具体位置，不要泛泛而谈）：
1. 布局结构：顶部标题/提示行、左侧"可用方案"列表(底部搜索框+新建按钮)、右侧方案名+右上4个图标按钮、右侧双栏内容区(左=携带清单条目表格、右=可选择项列表)、底部关闭按钮——是否齐全、位置合理、有无重叠越界。
2. 文字问题：任何文字截断、重叠、超出所在框/列边界、贴边过近，指出是哪段文字。
3. 对齐问题：左栏行内 图标/名称/数字输入框/灰字/红色删除按钮 的垂直对齐与行高；右栏复选框与名称对齐；表头与列对齐。
4. 特殊字符：右上角按钮里的 ★ ✎ ⧉ × 是否正常显示，是否有方块乱码(tofu)；下拉框的 ▾ 是否正常。
5. 配色可读性：对比度、奇数行高亮可辨性、勾选框清晰度。
6. 其他拥挤/不对劲/不符合RimWorld习惯之处。
最后给一个总体结论：可交付 / 需修改(列出必改项)。"""


def main():
    img_path = sys.argv[1]
    model = sys.argv[2] if len(sys.argv) > 2 else "nvidia/nemotron-3-nano-omni-30b-a3b-reasoning:free"
    key = os.environ["OPENROUTER_API_KEY"]
    with open(img_path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode()
    body = {
        "model": model,
        "messages": [{
            "role": "user",
            "content": [
                {"type": "text", "text": PROMPT},
                {"type": "image_url",
                 "image_url": {"url": f"data:image/png;base64,{b64}"}},
            ],
        }],
    }
    req = urllib.request.Request(
        API, data=json.dumps(body).encode(),
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=180) as r:
        resp = json.load(r)
    if "choices" not in resp:
        print(json.dumps(resp, ensure_ascii=False)[:2000])
        return
    print(resp["choices"][0]["message"]["content"])


if __name__ == "__main__":
    main()
