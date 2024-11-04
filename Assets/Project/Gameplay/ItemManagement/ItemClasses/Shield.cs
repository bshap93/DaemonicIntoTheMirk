﻿using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using MoreMountains.TopDownEngine;
using Project.Gameplay.ItemManagement.ItemUseAbilities;
using TopDownEngine.Common.Scripts.Characters.Core;
using UnityEngine;

namespace Project.Gameplay.ItemManagement.ItemClasses
{
    /// <summary>
    ///     Shield base class that handles shield mechanics and states
    /// </summary>
    [SelectionBase]
    public class Shield : MMMonoBehaviour
    {
        public enum ShieldStates
        {
            ShieldIdle,
            ShieldStart,
            ShieldActive,
            ShieldBlock,
            ShieldBreak,
            ShieldRecover,
            ShieldInterrupted
        }

        [MMInspectorGroup("ID", true, 7)] [Tooltip("the name of the shield")]
        public string ShieldName;

        [MMInspectorGroup("Shield Properties", true, 8)]
        [Tooltip("The amount of damage this shield can block before breaking")]
        public float MaxShieldHealth = 100f;
        [Tooltip("Current shield durability")] public float CurrentShieldHealth;
        [Tooltip("Time required for the shield to recover after breaking")]
        public float RecoveryTime = 5f;
        [Tooltip("Damage reduction percentage when blocking (0-1)")]
        public float BlockingDamageReduction = 0.5f;

        [MMInspectorGroup("Movement", true, 9)] [Tooltip("Movement speed multiplier while shield is raised")]
        public float MovementMultiplier = 0.5f;
        [Tooltip("if true, shield will slow movement while active")]
        public bool ModifyMovementWhileBlocking = true;

        [MMInspectorGroup("Feedback", true, 10)]
        public MMFeedbacks ShieldRaiseFeedback;
        public MMFeedbacks ShieldBlockFeedback;
        public MMFeedbacks ShieldBreakFeedback;

        [MMInspectorGroup("Animation", true, 11)]
        public string ShieldUpAnimationParameter = "ShieldUp";
        public string ShieldBlockAnimationParameter = "ShieldBlock";
        public string ShieldBreakAnimationParameter = "ShieldBreak";

        protected HashSet<int> _animatorParameters;
        protected CharacterMovement _characterMovement;
        protected bool _initialized;
        protected float _movementMultiplierStorage = 1f;

        protected Animator _ownerAnimator;
        protected int _shieldBlockAnimationParameter;
        protected int _shieldBreakAnimationParameter;

        protected int _shieldUpAnimationParameter;
        public MMStateMachine<ShieldStates> ShieldState;

        // References
        public Character Owner { get; protected set; }
        public CharacterHandleShield CharacterHandle { get; set; }

        public virtual void Initialization()
        {
            if (_initialized) return;

            ShieldState = new MMStateMachine<ShieldStates>(gameObject, true);
            ShieldState.ChangeState(ShieldStates.ShieldIdle);
            CurrentShieldHealth = MaxShieldHealth;

            InitializeFeedbacks();
            _initialized = true;
        }

        protected virtual void InitializeFeedbacks()
        {
            ShieldRaiseFeedback?.Initialization(gameObject);
            ShieldBlockFeedback?.Initialization(gameObject);
            ShieldBreakFeedback?.Initialization(gameObject);
        }

        public virtual void SetOwner(Character newOwner, CharacterHandleShield handleShield)
        {
            Owner = newOwner;
            CharacterHandle = handleShield;

            if (Owner != null)
            {
                _characterMovement = Owner.FindAbility<CharacterMovement>();
                _ownerAnimator = handleShield.CharacterAnimator;
            }

            InitializeAnimatorParameters();
        }

        protected virtual void InitializeAnimatorParameters()
        {
            if (_ownerAnimator == null) return;

            _animatorParameters = new HashSet<int>();

            RegisterAnimatorParameter(
                ShieldUpAnimationParameter, AnimatorControllerParameterType.Bool, out _shieldUpAnimationParameter);

            RegisterAnimatorParameter(
                ShieldBlockAnimationParameter, AnimatorControllerParameterType.Trigger,
                out _shieldBlockAnimationParameter);

            RegisterAnimatorParameter(
                ShieldBreakAnimationParameter, AnimatorControllerParameterType.Trigger,
                out _shieldBreakAnimationParameter);
        }

        protected virtual void RegisterAnimatorParameter(string parameterName,
            AnimatorControllerParameterType parameterType, out int parameter)
        {
            parameter = Animator.StringToHash(parameterName);

            if (_ownerAnimator == null) return;

            if (_ownerAnimator.MMHasParameterOfType(parameterName, parameterType)) _animatorParameters.Add(parameter);
        }

        public virtual void RaiseShield()
        {
            Debug.Log($"RaiseShield called. Current state: {ShieldState.CurrentState}");

            if (ShieldState.CurrentState == ShieldStates.ShieldBreak)
            {
                Debug.Log("Shield is broken, cannot raise");
                return;
            }

            ShieldState.ChangeState(ShieldStates.ShieldActive);
            ShieldRaiseFeedback?.PlayFeedbacks();
            
            Debug.Log("Shield raised successfully");


            if (_characterMovement != null && ModifyMovementWhileBlocking)
            {
                _movementMultiplierStorage = _characterMovement.MovementSpeedMultiplier;
                _characterMovement.MovementSpeedMultiplier = MovementMultiplier;
            }
        }

        public virtual void LowerShield()
        {
            if (ShieldState.CurrentState == ShieldStates.ShieldBreak) return;

            ShieldState.ChangeState(ShieldStates.ShieldIdle);

            if (_characterMovement != null && ModifyMovementWhileBlocking)
                _characterMovement.MovementSpeedMultiplier = _movementMultiplierStorage;
        }

        public virtual bool ProcessDamage(float incomingDamage)
        {
            if (ShieldState.CurrentState != ShieldStates.ShieldActive) return false;

            var reducedDamage = incomingDamage * (1f - BlockingDamageReduction);
            CurrentShieldHealth -= reducedDamage;

            ShieldBlockFeedback?.PlayFeedbacks();
            MMAnimatorExtensions.UpdateAnimatorTrigger(
                _ownerAnimator, _shieldBlockAnimationParameter, _animatorParameters);

            if (CurrentShieldHealth <= 0) BreakShield();

            return true;
        }

        protected virtual void BreakShield()
        {
            ShieldState.ChangeState(ShieldStates.ShieldBreak);
            ShieldBreakFeedback?.PlayFeedbacks();
            MMAnimatorExtensions.UpdateAnimatorTrigger(
                _ownerAnimator, _shieldBreakAnimationParameter, _animatorParameters);

            StartCoroutine(RecoverShieldCoroutine());
        }

        protected virtual IEnumerator RecoverShieldCoroutine()
        {
            yield return new WaitForSeconds(RecoveryTime);
            CurrentShieldHealth = MaxShieldHealth;
            ShieldState.ChangeState(ShieldStates.ShieldIdle);
        }


        public virtual void UpdateAnimator()
        {
            if (_ownerAnimator == null || _animatorParameters == null) return;

            var shieldUp = ShieldState.CurrentState == ShieldStates.ShieldActive;
            MMAnimatorExtensions.UpdateAnimatorBool(
                _ownerAnimator, _shieldUpAnimationParameter, shieldUp, _animatorParameters);
        }
    }
}