import io

p = "Assets/Scripts/Battle/Fusion/FusionController.cs"
s = io.open(p, encoding="utf-8").read()

# 1) BuildHighlights block: remove unconditional Play, default hidden
old_build = """            var animGo = new GameObject("SelectedFX");
            animGo.transform.SetParent(go.transform, false);

            var animRT = animGo.AddComponent<RectTransform>();
            animRT.anchorMin = animRT.anchorMax = new Vector2(0.5f, 0.5f);
            animRT.pivot = new Vector2(0.5f, 0.5f);
            animRT.anchoredPosition = Vector2.zero;
            animRT.sizeDelta = rt.sizeDelta * 200.0f;   // 与高亮方块完全同尺寸（盖住数字）

            var animImg = animGo.AddComponent<Image>();
            animImg.color = Color.white;
            animImg.raycastTarget = false;   // 不挡高亮点按
            animImg.sprite = null;             // 由动画逐帧驱动

            var anim = animGo.AddComponent<Animation>();
            anim.clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animation/Selected.anim");
            anim.Play();

            var numGo = new GameObject("Num");
"""
new_build = """			var animGo = new GameObject("SelectedFX");
			animGo.transform.SetParent(go.transform, false);

			var animRT = animGo.AddComponent<RectTransform>();
			animRT.anchorMin = animRT.anchorMax = new Vector2(0.5f, 0.5f);
			animRT.pivot = new Vector2(0.5f, 0.5f);
			animRT.anchoredPosition = Vector2.zero;
			animRT.sizeDelta = rt.sizeDelta*50.0f;   // 与高亮方块完全同尺寸（盖住数字）

			var animImg = animGo.AddComponent<Image>();
			animImg.color = Color.white;
			animImg.raycastTarget = false;   // 不挡高亮点按
			animImg.sprite = null;             // 由动画逐帧驱动

			var anim = animGo.AddComponent<Animation>();
			anim.clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animation/Selected.anim");
			animGo.SetActive(false);   // 默认隐藏：选中时才显示并播放
			animGo.transform.SetAsLastSibling(); // 渲染层级提到 Num 之上（盖住数字）

			var numGo = new GameObject("Num");
"""
assert old_b in s
s = s.replace(old_b, new_b, 1)

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
assert old in s
s = s.replace(old, new, 1)

open(p, "w", encoding="utf-8").write(s)
print("PATCH OK")