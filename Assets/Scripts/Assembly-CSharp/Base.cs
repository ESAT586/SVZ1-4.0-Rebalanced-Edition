using UnityEngine;

public class Base : Character
{
	public Base(GameObject baseObject)
	{
		base.isBase = true;
		ActivateHealthBar();
		base.controlledObject = baseObject;
		base.controller.Idle();
		SetupBaseAttributes();
	}

	public override void Update()
	{
		base.Update();
	}

	public override void Destroy()
	{
		base.Destroy();
	}

	public void Revive()
	{
		base.controller.startedDieAnim = false;
		if (base.health <= 0f)
		{
			base.controller.StopWalking();
			base.controller.Idle();
			base.controller.PlayHurtAnim("revive");
		}
		base.health = base.maxHealth;
	}

	private void SetupBaseAttributes()
	{
		string registryPath = (Singleton<PlayModesManager>.instance != null && Singleton<PlayModesManager>.instance.selectedMode == "zombies") ? "Registry/Base_ZM" : "Registry/Base";
		SDFTreeNode sDFTreeNode = SingletonMonoBehaviour<ResourcesManager>.instance.Open(registryPath) ?? SingletonMonoBehaviour<ResourcesManager>.instance.Open("Registry/Base");
		if (sDFTreeNode == null)
		{
			base.maxHealth = 250f;
			base.health = base.maxHealth;
			return;
		}

		int level = Mathf.Clamp(Singleton<Profile>.instance.baseLevel, 1, Mathf.Max(1, sDFTreeNode.childCount));
		SDFTreeNode sDFTreeNode2 = sDFTreeNode.to(string.Format("{0:000}", level)) ?? sDFTreeNode.to("001");
		if (sDFTreeNode2 == null || !sDFTreeNode2.hasAttribute("health"))
		{
			base.maxHealth = 250f;
			base.health = base.maxHealth;
			return;
		}
		base.maxHealth = float.Parse(sDFTreeNode2["health"]);
		base.health = base.maxHealth;
	}
}
