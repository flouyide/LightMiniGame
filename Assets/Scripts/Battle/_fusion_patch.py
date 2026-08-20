import io

p = "Assets/Scripts/Battle/FusionController.cs"
s = io.open(p, encoding="utf-8").read()

# 1) BuildHighlights block: remove unconditional Play, hidden by default
old_build = (
    "            var animGo = new GameObject(\"SelectedFX\");\n"
    "            animGo.transform.SetParent(go.transform, false);\n"
    "\n"
    "            var animRT = animGo.AddComponent<RectTransform>();\n"
    "            animRT.anchorMin = animRT.anchorMax = new Vector2(0.5f, 0.5f);\n"
    "            animRT.pivot = new Vector2(0.5f, 0.5f);\n"
    "            animRT.anchoredPosition = Vector2.zero;\n"
    "            animRT.sizeDelta = rt.sizeDelta;\n"
    "\n"
    "            var animImg = animGo.AddComponent<Image>();\n"
    "            animImg.color = Color.white;\n"
    "            animImg.raycastTarget = false;\n"
    "            animImg.sprite = null;\n"
    "\n"
    "            var anim = animGo.AddComponent<Animation>();\n"
    "            anim.clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(\"Assets/Art/Animation/Selected.anim\");\n"
    "            animGo.SetActive(false);\n"
    "            animGo.transform.SetAsLastSibling();\n"
    "\n"
    "            var numGo = new GameObject(\"Num\");\n"
)
new_build = (
    "            var animGo = new GameObject(\"SelectedFX\");\n"
    "            animGo.transform.SetParent(go.transform, false);\n"
    "\n"
    "            var animRT = animGo.AddComponent<RectTransform>();\n"
    "            animRT.anchorMin = animRT.anchorMax = new Vector2(0.5f, 0.5f);\n"
    "            animRT.pivot = new Vector2(0.5f, 0.5f);\n"
    "            animRT.anchoredPosition = Vector2.zero;\n"
    "            animRT.sizeDelta = rt.sizeDelta;\n"
    "\n"
    "            var animImg = animGo.AddComponent<Image>();\n"
    "            animImg.color = Color.white;\n"
    "            animImg.raycastTarget = false;\n"
    "            animImg.sprite = null;\n"
    "\n"
    "            var anim = animGo.AddComponent<Animation>();\n"
    "            anim.clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(\"Assets/Art/Animation/Selected.anim\");\n"
    "            animGo.SetActive(false);\n"
    "            animGo.transform.SetAsLastSibling();\n"
    "\n"
    "            var numGo = new GameObject(\"Num\");\n"
)
assert old_build in s
s = s.replace(old_build, new_build, 1)

# 2) OnHighlightClick: show/hide + Rewind/Play vs Stop
old_click = """		if (_selected.Contains(fv)) { _selected.Remove(fv); }
		else { _selected.Add(fv); }

		RefreshHighlights();
		UpdateStatus();"""
new_click = """		if (_selected.Contains(fv)) { _selected.Remove(fv); }
			else { _selected.Add(fv); }

			RefreshHighlights();
			UpdateStatus();
			SyncSelectedFX();"""
assert old_click in s
s = s.replace(old_click, new_click, 1)

# 3) RefreshHighlights: sync show/hide
old_refresh = """			var num = go.transform.Find("Num")?.GetComponent<TextMeshProUGUI>();
			if (num != null)
				num.color = _selected.Contains(_candidates[i])
					? new Color(1f, 0.23f, 0.36f, 1f)
					: new Color(0.75f, 0.29f, 1f, 1f);
		}
	}"""
new_refresh = """			var num = go.transform.Find("Num")?.GetComponent<TextMeshProUGUI>();
			if (num != null)
				num.color = _selected.Contains(_candidates[i])
					? new Color(1f, 0.23f, 0.36f, 1f)
					: new Color(0.75f, 0.29f, 1f, 1f);
			// 选中动画显隐：与 _selected 同步
			var fx = go.transform.Find("SelectedFX");
			if (fx != null)
			{
				bool sel = _selected.Contains(_candidates[i]);
				fx.gameObject.SetActive(sel);
				if (sel)
				{
					var a = fx.GetComponent<Animation>();
					if (a != null) { a.Rewind(); a.Play(); }
				}
				else
				{
					var a2 = fx.GetComponent<Animation>();
					if (a2 != null) a2.Stop();
				}
			}
		}
	}"""
assert old_refresh in s
s = s.replace(old_refresh, new_refresh, 1)

open(p, "w", encoding="utf-8").write(s)
print("PATCH OK")
Directory: (root)
Output:
Traceback (most recent call last):
  File "<stdin>", line 96, in <module>
    print("PATCH OK")
SyntaxError: EOF while scanning triple-quoted string literal
Error: (none)
Exit Code: 1
Signal: (none)
Directory: (root)
Output:
(empty)
Error: (none)
Exit Code: 0
Signal: (none)
