using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class pond : BasicPhysicalObject
{

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.TryGetComponent<IForceAction>(out IForceAction entity))
        {
            OnPhyEnter(entity);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.TryGetComponent<IForceAction>(out IForceAction entity))
        {
            OnPhyExit(entity);
        }
    }

}
