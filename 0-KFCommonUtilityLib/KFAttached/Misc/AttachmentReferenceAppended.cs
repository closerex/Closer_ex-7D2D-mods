using UnityEngine;

public class AttachmentReferenceAppended : AttachmentReference
{
    private Transform[] bindings;
    public void Merge(AnimationTargetsAbs targets)
    {
        if (attachmentReference && targets)
        {
            foreach (var bindings in attachmentReference.GetComponentsInChildren<TransformActivationBinding>(true))
            {
                bindings.targets = targets;
            }
            bindings = new Transform[attachmentReference.childCount];
            for (int i = 0; i < attachmentReference.childCount; i++)
            {
                bindings[i] = attachmentReference.GetChild(i);
                bindings[i].SetParent(targets.AttachmentRef, false);
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