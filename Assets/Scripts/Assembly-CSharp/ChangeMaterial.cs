using UnityEngine;

public class ChangeMaterial : MonoBehaviour
{
	public Material[] materials;

	private void Start()
	{
		if (!MultiLanguages.isMultiLanguages)
		{
			return;
		}
		ParticleRenderer component = GetComponent<ParticleRenderer>();
		if (!(bool)component)
		{
			return;
		}

		Material localizedMaterial = GetLocalizedMaterial();
		if (localizedMaterial == null)
		{
			return;
		}

		component.material = localizedMaterial;
	}

	private void Update()
	{
	}

	private Material GetLocalizedMaterial()
	{
		if (materials == null || materials.Length == 0)
		{
			return null;
		}

		int num = 0;
		switch (MultiLanguages.GetCurrentLanguageToEnum())
		{
		case MultiLanguages.ESystemLanguage.ESL_Chinese:
			num = 1;
			break;
		case MultiLanguages.ESystemLanguage.ESL_Korean:
			num = 2;
			break;
		case MultiLanguages.ESystemLanguage.ESL_Japanese:
			num = 3;
			break;
		}

		if (num >= 0 && num < materials.Length && materials[num] != null)
		{
			return materials[num];
		}

		for (int i = 0; i < materials.Length; i++)
		{
			if (materials[i] != null)
			{
				return materials[i];
			}
		}

		return null;
	}
}

