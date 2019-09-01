﻿using Aurora.EffectsEngine;
using Aurora.Settings;
using Aurora.Settings.Layers;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Aurora.EffectsEngine.Animations;
using Aurora.Profiles.EliteDangerous.GSI;
using Aurora.Profiles.EliteDangerous.GSI.Nodes;
using Aurora.Utils;

namespace Aurora.Profiles.EliteDangerous.Layers
{
    public enum EliteAnimation
    {
        None,
        FsdCountdowm,
        Hyperspace,
        StarEntry,
    }
    public class EliteDangerousAnimationHandlerProperties : LayerHandlerProperties2Color<EliteDangerousAnimationHandlerProperties>
    {
        public EliteDangerousAnimationHandlerProperties() : base() { }

        public EliteDangerousAnimationHandlerProperties(bool assign_default = false) : base(assign_default) { }

        public override void Default()
        {
            base.Default();
        }
    }
    public class EliteDangerousAnimationLayerHandler : LayerHandler<EliteDangerousAnimationHandlerProperties>
    {
        private AnimationMix fsd_countdown_mix;
        private AnimationMix hyperspace_mix;
        private AnimationMix star_entry_mix;
        
        private long previousTime = Time.GetMillisecondsSinceEpoch();
        private long currentTime = Time.GetMillisecondsSinceEpoch();

        private float getDeltaTime()
        {
            return (currentTime - previousTime) / 1000.0f;
        }

        private float layerFadeState = 0;
        private static float totalAnimationTime, animationKeyframe = 0.0f;
        private static EliteAnimation currentAnimation = EliteAnimation.None;
        private static float animationTime = 0.0f;
        private EliteAnimation animateOnce = EliteAnimation.None;
        
        public EliteDangerousAnimationLayerHandler() : base()
        {
            _ID = "EliteDangerousAnimations";
            UpdateAnimations();
        }

        protected override UserControl CreateControl()
        {
            return new Control_EliteDangerousAnimationLayer(this);
        }
        
        static float findMod(float a, float b) 
        { 
          
            // Handling negative values 
            if (a < 0) 
                a = -a; 
            if (b < 0) 
                b = -b; 
      
            // Finding mod by repeated subtraction 
            float mod = a; 
            while (mod >= b) 
                mod = mod - b; 
      
            // Sign of result typically depends 
            // on sign of a. 
            if (a < 0) 
                return -mod; 
      
            return mod; 
        }

        private void BgFadeIn(EffectLayer animation_layer)
        {
            layerFadeState = Math.Min(1, layerFadeState + 0.07f);
            animation_layer.Fill(ColorUtils.BlendColors(Color.Empty, Color.Black, layerFadeState));
        }

        private void BgFadeOut(EffectLayer animation_layer)
        {
            if (!(layerFadeState > 0)) return;
            layerFadeState = Math.Max(0, layerFadeState - 0.03f);
            animation_layer.Fill(ColorUtils.BlendColors(Color.Empty, Color.Black, layerFadeState));
        }

        public override EffectLayer Render(IGameState state)
        {
            GameState_EliteDangerous gameState = state as GameState_EliteDangerous;

            previousTime = currentTime;
            currentTime = Time.GetMillisecondsSinceEpoch();

            EffectLayer animation_layer = new EffectLayer("Elite: Dangerous - Animations");
            
            if (gameState.Journal.exitStarType != StarType.None)
            {
                gameState.Journal.exitStarType = StarType.None;
                animateOnce = EliteAnimation.StarEntry;
                totalAnimationTime = 0;
                animationKeyframe = 0;
            }

            if (animateOnce != EliteAnimation.None)
            {
                currentAnimation = animateOnce;
            } else if (gameState.Journal.fsdState == FSDState.Idle)
            {
                currentAnimation = EliteAnimation.None;
            } else if (gameState.Journal.fsdState == FSDState.CountdownSupercruise || gameState.Journal.fsdState == FSDState.CountdownHyperspace)
            {
                currentAnimation = EliteAnimation.FsdCountdowm;
            } else if (gameState.Journal.fsdState == FSDState.InHyperspace)
            {
                currentAnimation = EliteAnimation.Hyperspace;
            }
            
            if (currentAnimation == EliteAnimation.None)
            {
                animationKeyframe = 0;
                totalAnimationTime = 0;
            }

            if (currentAnimation != EliteAnimation.None || gameState.Journal.fsdWaitingSupercruise)
            {
                BgFadeIn(animation_layer);
            }
            else if (layerFadeState > 0)
            {
                BgFadeOut(animation_layer);
            }

            float deltaTime = 0f, currentAnimationDuration = 0f;
            if(currentAnimation == EliteAnimation.FsdCountdowm) {
                currentAnimationDuration = fsd_countdown_mix.GetDuration();
                fsd_countdown_mix.Draw(animation_layer.GetGraphics(), animationKeyframe);
                deltaTime = getDeltaTime();
                animationKeyframe += deltaTime;
            } else if (currentAnimation == EliteAnimation.Hyperspace)
            {
                currentAnimationDuration = hyperspace_mix.GetDuration();
                hyperspace_mix.Draw(animation_layer.GetGraphics(), animationKeyframe);
                hyperspace_mix.Draw(animation_layer.GetGraphics(), findMod(animationKeyframe + 1.2f, currentAnimationDuration));
                hyperspace_mix.Draw(animation_layer.GetGraphics(), findMod(animationKeyframe + 2.8f, currentAnimationDuration));
                deltaTime = getDeltaTime();
                //Loop the animation
                animationKeyframe = findMod(animationKeyframe + (deltaTime), currentAnimationDuration);
               
            } else if (currentAnimation == EliteAnimation.StarEntry)
            {
                currentAnimationDuration = star_entry_mix.GetDuration();
                star_entry_mix.Draw(animation_layer.GetGraphics(), animationKeyframe);
                deltaTime = getDeltaTime();

                animationKeyframe += deltaTime;
            }
            
            totalAnimationTime += deltaTime;
            if (totalAnimationTime > currentAnimationDuration)
            {
                animateOnce = EliteAnimation.None;
            }

            return animation_layer;
        }

        public override void SetApplication(Application profile)
        {
            (Control as Control_EliteDangerousAnimationLayer).SetProfile(profile);
            base.SetApplication(profile);
        }

        public void UpdateAnimations()
        {
            fsd_countdown_mix = new AnimationMix();
            Color pulseStartColor = Color.FromArgb(0, 126, 255);
            Color pulseEndColor = Color.FromArgb(200, 0, 126, 255);

            float startingX = Effects.canvas_width_center - 10;
            int pulseStartWidth = 10;
            int pulseEndWidth = 2;
            
            float pulseFrameDuration = 1;
            float pulseDuration = 0.7f;
            
            AnimationTrack countdown_pulse_1 = new AnimationTrack("Fsd countdown pulse 1", pulseFrameDuration);
            countdown_pulse_1.SetFrame(0.0f,
                new AnimationCircle(startingX, Effects.canvas_height_center, 0, pulseStartColor, pulseStartWidth)
            );
            countdown_pulse_1.SetFrame(pulseDuration,
                new AnimationCircle(startingX, Effects.canvas_height_center, Effects.canvas_biggest, pulseEndColor, pulseEndWidth)
            );
            
            AnimationTrack countdown_pulse_2 = new AnimationTrack("Fsd countdown pulse 2", pulseFrameDuration, 1);
            countdown_pulse_2.SetFrame(0.0f,
                new AnimationCircle(startingX, Effects.canvas_height_center, 0, pulseStartColor, pulseStartWidth)
            );
            countdown_pulse_2.SetFrame(pulseDuration,
                new AnimationCircle(startingX, Effects.canvas_height_center, Effects.canvas_biggest, pulseEndColor, pulseEndWidth)
            );
            
            AnimationTrack countdown_pulse_3 = new AnimationTrack("Fsd countdown pulse 3", pulseFrameDuration, 2);
            countdown_pulse_3.SetFrame(0.0f,
                new AnimationCircle(startingX, Effects.canvas_height_center, 0, pulseStartColor, pulseStartWidth)
            );
            countdown_pulse_3.SetFrame(pulseDuration,
                new AnimationCircle(startingX, Effects.canvas_height_center, Effects.canvas_biggest, pulseEndColor, pulseEndWidth)
            );
            
            AnimationTrack countdown_pulse_4 = new AnimationTrack("Fsd countdown pulse 4", pulseFrameDuration, 3);
            countdown_pulse_4.SetFrame(0.0f,
                new AnimationCircle(startingX, Effects.canvas_height_center, 0, pulseStartColor, pulseStartWidth)
            );
            countdown_pulse_4.SetFrame(pulseDuration,
                new AnimationCircle(startingX, Effects.canvas_height_center, Effects.canvas_biggest, pulseEndColor, pulseEndWidth)
            );
            
            AnimationTrack countdown_pulse_5 = new AnimationTrack("Fsd countdown pulse 5", pulseFrameDuration, 4);
            countdown_pulse_5.SetFrame(0.0f,
                new AnimationCircle(startingX, Effects.canvas_height_center, 0, pulseStartColor, pulseStartWidth)
            );
            countdown_pulse_5.SetFrame(pulseDuration,
                new AnimationCircle(startingX, Effects.canvas_height_center, Effects.canvas_biggest, pulseEndColor, pulseEndWidth)
            );

            fsd_countdown_mix.AddTrack(countdown_pulse_1);
            fsd_countdown_mix.AddTrack(countdown_pulse_2);
            fsd_countdown_mix.AddTrack(countdown_pulse_3);
            fsd_countdown_mix.AddTrack(countdown_pulse_4);
            fsd_countdown_mix.AddTrack(countdown_pulse_5);
            fsd_countdown_mix.AddTrack(new AnimationTrack("Fsd countdown delay", pulseFrameDuration, 4));
            
            hyperspace_mix = new AnimationMix();
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 0, 1.5f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 5, 0.1f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 12, 2.1f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 15, 2.7f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 20, 0.7f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 25, 2.4f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 30, 1.4f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 35, 0.3f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 40, 1.8f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 45, 1.0f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 50, 2.5f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 55, 1.5f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 60, 0.9f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 64, 2.3f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 68, 1.9f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 77, 0.0f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 82, 1.1f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 85, 1.3f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 93, 2.1f, hyperspace_mix.GetTracks().Count));
            hyperspace_mix.AddTrack(GenerateHyperspaceStreak(Effects.canvas_width / 100 * 100, 0.4f, hyperspace_mix.GetTracks().Count));
            
            star_entry_mix = new AnimationMix();
            Color starEntryColor = Color.FromArgb(255, 140, 0);
            AnimationTrack star_entry = new AnimationTrack("Star entry", 2.0f);
            star_entry.SetFrame(0.0f,
                new AnimationFilledCircle(startingX, Effects.canvas_height_center, 0, starEntryColor, 1)
            );
            star_entry.SetFrame(1.2f,
                new AnimationFilledCircle(startingX, Effects.canvas_height_center, Effects.canvas_biggest, starEntryColor, 1)
            );
            star_entry.SetFrame(2f,
                new AnimationFilledCircle(startingX, Effects.canvas_height_center, Effects.canvas_biggest, Color.Empty, 1)
            );
            star_entry_mix.AddTrack(star_entry);
        }

        float hyperspaceAnimationDuration = 0.8f; 
        private AnimationTrack GenerateHyperspaceStreak(float xOffset, float timeShift, int index = 0)
        {
            Color streakEndColor = Color.FromArgb(178, 217, 255);
            Color streakStartColor = Color.FromArgb(0, 64, 135);

            int streakSize = 7;
            int streakWidth = 3;

            int startPosition = -40;
            int endPosition = Effects.canvas_height + streakSize * 2;
            
            AnimationTrack streak = new AnimationTrack("Hyperspace streak " + index, hyperspaceAnimationDuration, timeShift);
            streak.SetFrame(0.0f,
                new AnimationLine(new PointF(xOffset, startPosition), new PointF(xOffset, startPosition + streakSize), streakStartColor, streakEndColor, streakWidth)
            );
            streak.SetFrame(hyperspaceAnimationDuration,
                new AnimationLine(new PointF(xOffset, endPosition), new PointF(xOffset, endPosition + streakSize), streakStartColor, streakEndColor, streakWidth)
            );

            return streak;
        }
    }
}
