using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class AnimateHandController : MonoBehaviour
{
    // Start is called before the first frame update
    public InputActionReference gripInputActionRefference;
    public InputActionReference triggerInputActionRefference;

    private Animator _handAnimator;
    private float _gripValue;
    private float _triggerValue;

    void Start()
    {
        _handAnimator = GetComponent <Animator> ();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void AnimateGrip()
    {
        _gripValue = gripInputActionRefference.action.ReadValue<float>();
        _handAnimator.SetFloat("Grip",_gripValue);
    }

    private void AnimateTrigger()
    {
        _triggerValue = triggerInputActionRefference.action.ReadValue<float>();
        _handAnimator.SetFloat("Trigger", _gripValue);
    }
}