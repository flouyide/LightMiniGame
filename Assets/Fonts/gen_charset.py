chars = []

# ASCII 可打印字符 (32-126)
for i in range(32, 127):
    chars.append(chr(i))

# 中文常用标点 (用 Unicode 码点避免引号冲突)
punct_codes = [
    0xFF0C, 0x3002, 0x3001, 0xFF1B, 0xFF1A, 0xFF1F, 0xFF01,  # ，。、；：？！
    0x201C, 0x201D, 0x2018, 0x2019,  # " " ' '
    0xFF08, 0xFF09, 0x3010, 0x3011, 0x300A, 0x300B,  # （）【】《》
    0x2014, 0x2026, 0x00B7, 0xFF5E, 0x300C, 0x300D, 0x300E, 0x300F,  # —…·～「」『』
    0x3008, 0x3009,  # 〈〉
]
for code in punct_codes:
    chars.append(chr(code))

# GB2312 一级常用汉字 (3755字)
for high in range(0xB0, 0xD8):
    for low in range(0xA1, 0xFF):
        try:
            char = bytes([high, low]).decode('gb2312')
            if char not in chars:
                chars.append(char)
        except Exception:
            pass

# GB2312 二级汉字 (3008字)
for high in range(0xD8, 0xF8):
    for low in range(0xA1, 0xFF):
        try:
            char = bytes([high, low]).decode('gb2312')
            if char not in chars:
                chars.append(char)
        except Exception:
            pass

result = ''.join(chars)
out = r'F:\unity projects\LightMiniGame\Assets\Fonts\ChineseCharacters.txt'
with open(out, 'w', encoding='utf-8') as f:
    f.write(result)

ascii_count = sum(1 for c in chars if ord(c) < 128)
cn_count = len(chars) - ascii_count
print(f'文件已生成: {out}')
print(f'总字符数: {len(chars)} (ASCII {ascii_count} + 中文/标点 {cn_count})')
