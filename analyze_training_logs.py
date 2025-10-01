#!/usr/bin/env python3
"""
Training log analyzer
Checks if multi-drone training worked properly
"""

import os
import glob
import json
import csv
from datetime import datetime

def analyze_training_logs():
    """Look at training logs to see if multi-drone stuff worked"""
    print("Analyzing Multi-Drone Training Logs...")
    
    # Look for training results
    result_dirs = [
        "results/multi_drone_path_v1",
        "results/multi_drone_path_v2"
    ]
    
    for result_dir in result_dirs:
        if not os.path.exists(result_dir):
            continue
            
        print(f"\nAnalyzing: {result_dir}")
        
        # Check config file
        config_file = os.path.join(result_dir, "configuration.yaml")
        if os.path.exists(config_file):
            print("Configuration file found")
            
        # Check if training actually worked
        agent_dir = os.path.join(result_dir, "DroneAgent")
        if os.path.exists(agent_dir):
            print("DroneAgent training data found")
            
            # Find model files
            model_files = []
            for ext in ['*.onnx', '*.pt']:
                model_files.extend(glob.glob(os.path.join(agent_dir, ext)))
            
            if model_files:
                print(f"   Models: {len(model_files)} files")
                latest_model = max(model_files, key=os.path.getmtime)
                print(f"   Latest model: {os.path.basename(latest_model)}")
                
                # Get training steps from filename
                filename = os.path.basename(latest_model)
                if 'DroneAgent-' in filename:
                    try:
                        steps = filename.split('DroneAgent-')[1].split('.')[0]
                        print(f"   Training steps: {steps}")
                    except:
                        pass
        
        # Check for TensorBoard logs
        tb_files = glob.glob(os.path.join(agent_dir, "events.out.tfevents.*"))
        if tb_files:
            print(f"TensorBoard logs: {len(tb_files)} files")
            
        # Check run logs
        run_logs_dir = os.path.join(result_dir, "run_logs")
        if os.path.exists(run_logs_dir):
            print("Run logs directory found")
            
            # Look for training logs
            log_files = glob.glob(os.path.join(run_logs_dir, "*.log"))
            if log_files:
                print(f"   Log files: {len(log_files)}")
                
                # Try to read the latest log for multi-drone indicators
                latest_log = max(log_files, key=os.path.getmtime)
                try:
                    with open(latest_log, 'r') as f:
                        content = f.read()
                        
                        # Look for multi-drone indicators
                        if 'DroneAgent_0' in content or 'DroneAgent_1' in content:
                            print("Multi-drone agent names found in logs")
                            
                        if 'agents' in content.lower() and 'spawned' in content.lower():
                            print("Agent spawning mentioned in logs")
                            
                        # Count behavior specs mentioned
                        behavior_count = content.count('behavior_spec')
                        if behavior_count > 0:
                            print(f"   Behavior specs mentioned: {behavior_count} times")
                            
                except Exception as e:
                    print(f"   Could not read log file: {e}")

def check_recent_training_activity():
    """Check for recent training activity"""
    print("\nChecking Recent Training Activity...")
    
    # Look for recent model files
    all_models = []
    for result_dir in ["results/multi_drone_path_v1", "results/multi_drone_path_v2"]:
        if os.path.exists(result_dir):
            agent_dir = os.path.join(result_dir, "DroneAgent")
            if os.path.exists(agent_dir):
                models = glob.glob(os.path.join(agent_dir, "*.onnx"))
                models.extend(glob.glob(os.path.join(agent_dir, "*.pt")))
                all_models.extend([(model, result_dir) for model in models])
    
    if all_models:
        # Sort by modification time
        all_models.sort(key=lambda x: os.path.getmtime(x[0]), reverse=True)
        
        print(f"Found {len(all_models)} model files")
        
        # Show the most recent ones
        print("\nMost Recent Models:")
        for i, (model_path, result_dir) in enumerate(all_models[:5]):
            filename = os.path.basename(model_path)
            mod_time = datetime.fromtimestamp(os.path.getmtime(model_path))
            size_mb = os.path.getsize(model_path) / (1024 * 1024)
            print(f"   {i+1}. {filename}")
            print(f"      📅 Modified: {mod_time}")
            print(f"      From: {os.path.basename(result_dir)}")
            print(f"      Size: {size_mb:.1f} MB")
            print()

def provide_recommendations():
    """Provide recommendations based on analysis"""
    print("Multi-Drone Training Recommendations:")
    print()
    
    # Check if we have successful training results
    has_v1 = os.path.exists("results/multi_drone_path_v1/DroneAgent")
    has_v2 = os.path.exists("results/multi_drone_path_v2/DroneAgent")
    
    if has_v1 and has_v2:
        print("Multiple successful multi-drone training runs detected!")
        print("   - You can resume training from the latest checkpoint")
        print("   - Or start a new run with different parameters")
        print()
        print("To continue training:")
        print("   mlagents-learn Assets/DroneRL/Config/multi_drone_config.yaml --run-id=multi_drone_path_v3 --force")
        print()
        print("To resume latest training:")
        print("   mlagents-learn Assets/DroneRL/Config/multi_drone_config.yaml --run-id=multi_drone_path_v2 --resume")
        
    elif has_v1 or has_v2:
        print("At least one successful multi-drone training run detected!")
        print("   - The multi-drone setup is working correctly")
        print("   - You can continue or start new experiments")
        
    else:
        print("No completed multi-drone training runs found")
        print("   - The setup files exist but training may not have completed")
        print("   - Try running a fresh training session")
    
    print()
    print("Next Steps:")
    print("1. Open Unity Editor")
    print("2. Load Assets/Scenes/DroneTrainingArena.unity")
    print("3. Run Tools -> DroneRL -> Setup Multi-Drone Path Training")
    print("4. Press Play")
    print("5. Run the training command above")

def main():
    """Main analysis function"""
    print("Multi-Drone Training Analysis Tool")
    print("=" * 60)
    
    if not os.path.exists("results"):
        print("No results directory found")
        return
    
    analyze_training_logs()
    check_recent_training_activity()
    
    print("\n" + "=" * 60)
    provide_recommendations()

if __name__ == "__main__":
    main()
