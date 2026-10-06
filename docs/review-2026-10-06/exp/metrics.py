# -*- coding: utf-8 -*-
"""结构度量启发式脚本：函数长度 + 粗略圈复杂度（评审用，非精确解析器）。
局限：正则识别方法签名，可能漏掉表达式体成员/泛型嵌套花括号字符串；
字符串/注释中的花括号未剥离（C# 花括号插值罕见于本项目）。结果为下界估计。"""
import os, re, sys, json, io

ROOT = r"C:\tjf\github\MyWordGame\Assets\Scripts"
DIRS = [os.path.join(ROOT, "Core"), os.path.join(ROOT, "Unity")]

# 方法签名启发式：可见性/修饰词开头，含 ( ，以 { 结尾的行
SIG = re.compile(
    r'^\s*(?:\[[^\]]*\]\s*)*'                       # 特性行
    r'((?:public|private|protected|internal|static|sealed|override|virtual|async|extern|unsafe|new|partial|readonly)\s+)+'
    r'(?:[\w<>\[\],\.\?:\s]+?)\s+'
    r'([\w<>]+)\s*\(',
    re.M)

KW = re.compile(r'\b(if|for|foreach|while|case|catch)\b|\&\&|\|\||\?\?|\?\.?.')

def strip_line_comment(l):
    i = l.find('//')
    if i >= 0 and not l[:i].rstrip().endswith(':'):  # 保守：URL 罕见
        # 检查是否在字符串内——简化：粗略保留
        return l
    return l

def analyze(path):
    src = io.open(path, encoding='utf-8-sig', errors='replace').read()
    lines = src.split('\n')
    methods = []
    # 找候选签名行
    for m in SIG.finditer(src):
        line_no = src[:m.start()].count('\n') + 1
        # 从签名行向后找第一个 {
        i = src.find('{', m.end() - 1)
        # 允许签名与 { 之间隔 max 2 行（参数换行）
        while i >= 0 and src[m.end()-1:i].count('\n') > 3:
            i = src.find('{', i + 1)
        if i < 0:
            continue
        # 确认 { 与 } 之间不是 expression-bodied（=> )：检查签名行是否已有 =>
        sig_end = src.rfind(';', m.start(), i)
        if src.find('=>', m.start(), i) >= 0 and src.find(';', m.start(), i) < i and src.find(';', m.start(), i) != -1:
            pass  # expression bodied -> skip 长度统计
        # 花括号配平
        depth = 0
        j = i
        instr = None
        while j < len(src):
            c = src[j]
            if instr:
                if c == '\\':
                    j += 2
                    continue
                if c == instr:
                    instr = None
            elif c in '"\'':
                instr = c
            elif c == '{':
                depth += 1
            elif c == '}':
                depth -= 1
                if depth == 0:
                    break
            j += 1
        body = src[i:j + 1]
        nlines = body.count('\n') + 1
        # 圈复杂度：决策点 +1
        body_nc = re.sub(r'//[^\n]*', '', body)
        body_nc = re.sub(r'"(?:[^"\\]|\\.)*"', '""', body_nc)
        cc = 1
        for dm in re.finditer(r'\b(if|for|foreach|while|case|catch)\b|&&|\|\|', body_nc):
            # else if 已被 if 计入；switch 的 case 每个计 1
            cc += 1
        methods.append({
            'file': path.replace(ROOT + os.sep, '').replace('\\', '/'),
            'name': m.group(2),
            'line': line_no,
            'lines': nlines,
            'cc': cc,
        })
    return methods

all_m = []
for d in DIRS:
    for dirpath, _, files in os.walk(d):
        for f in files:
            if f.endswith('.cs'):
                all_m.extend(analyze(os.path.join(dirpath, f)))

all_m.sort(key=lambda x: -x['lines'])
print("== TOP 25 函数长度 ==")
for m in all_m[:25]:
    print(f"{m['lines']:5d} 行  CC~{m['cc']:3d}  {m['file']}:{m['line']}  {m['name']}")

all_m.sort(key=lambda x: -x['cc'])
print("\n== TOP 25 圈复杂度 ==")
for m in all_m[:25]:
    print(f"CC~{m['cc']:3d}  {m['lines']:5d} 行  {m['file']}:{m['line']}  {m['name']}")

print(f"\n总方法数(启发式): {len(all_m)}")
print(f">=60 行方法数: {sum(1 for m in all_m if m['lines'] >= 60)}")
print(f">=15 CC 方法数: {sum(1 for m in all_m if m['cc'] >= 15)}")
over60 = {}
for m in all_m:
    if m['lines'] >= 60:
        over60[m['file']] = over60.get(m['file'], 0) + 1
print("\n== >=60 行方法分布（按文件）==")
for k, v in sorted(over60.items(), key=lambda kv: -kv[1]):
    print(f"{v:3d}  {k}")

with io.open(os.path.dirname(__file__) + '\\methods.json', 'w', encoding='utf-8') as f:
    json.dump(all_m, f, ensure_ascii=False)
