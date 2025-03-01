using UnityEngine;

public class AttachmentReferenceAppended : AttachmentReference
{
    private Transform[] bindings;
    public void Merge(Transform main, AnimationTargetsAbs targets)
    {
        if (attachmentReference && main)
        {
            foreach (var bindings in attachmentReference.GetComponentsInChildren<TransformActivationBinding>())
            {
                bindings.targets = targets;
            }
            bindings = new Transform[attachmentReference.childCount];
            for (int i = 0; i < attachmentReference.childCount; i++)
            {
                bindings[i] = attachmentReference.GetChild(i);
                bindings[i].SetParent(main, false);
            }
            Destroy(attachmentReference.gameObject);
            attachmentReference = null;
        }
    }

    public void Remove()
    {
        if (bindings != null)
        {
            foreach (var binding in bindings)
            {
                if (binding)
                {
                    binding.SetParent(null, false);
                    Destroy(binding.gameObject);
                }
            }
            bindings = null;
        }
        if (attachmentReference)
        {
            Destroy(attachmentReference.gameObject);
            attachmentReference = null;
        }
    }
}