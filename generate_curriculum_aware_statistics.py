#!/usr/bin/env python3
"""
Statistics generator that accounts for curriculum learning.
Looks at how performance changes as training gets harder.
"""

import argparse
import os
import pandas as pd
import numpy as np
from typing import Dict, List, Tuple, Optional
import glob

def parse_args():
    parser = argparse.ArgumentParser(description="Generate curriculum-aware statistical summary")
    parser.add_argument("--run_dirs", nargs='+', required=True, 
                       help="Path(s) to run directories with CSV files")
    parser.add_argument("--out_file", default="curriculum_aware_summary.txt", 
                       help="Output file for summary table")
    parser.add_argument("--stage_window", type=int, default=50,
                       help="Number of steps to average within each stage")
    return parser.parse_args()

def load_csv_data(csv_path: str) -> Tuple[List[int], List[float]]:
    """Load data from CSV file"""
    try:
        df = pd.read_csv(csv_path)
        return df['step'].tolist(), df['value'].tolist()
    except Exception as e:
        print(f"Warning: Could not load {csv_path}: {e}")
        return [], []

def find_stage_transitions(stage_steps: List[int], stage_values: List[float]) -> Dict[int, Tuple[int, int]]:
    """Figure out when curriculum stages changed"""
    if not stage_steps or not stage_values:
        return {}
    
    stage_ranges = {}
    current_stage = int(stage_values[0]) if stage_values else 0
    stage_start = stage_steps[0] if stage_steps else 0
    
    for i, (step, stage_val) in enumerate(zip(stage_steps, stage_values)):
        stage_num = int(stage_val)
        
        # Found a stage change
        if stage_num != current_stage:
            # Save the previous stage
            stage_ranges[current_stage] = (stage_start, stage_steps[i-1] if i > 0 else step)
            
            # Start tracking new stage
            current_stage = stage_num
            stage_start = step
    
    # Save the last stage
    stage_ranges[current_stage] = (stage_start, stage_steps[-1])
    
    return stage_ranges

def get_stage_performance(steps: List[int], values: List[float], 
                         stage_ranges: Dict[int, Tuple[int, int]], 
                         stage_window: int) -> Dict[int, Dict[str, float]]:
    """Calculate performance metrics for each curriculum stage"""
    stage_performance = {}
    
    for stage_num, (start_step, end_step) in stage_ranges.items():
        # Find values within this stage
        stage_values = []
        for step, value in zip(steps, values):
            if start_step <= step <= end_step:
                stage_values.append(value)
        
        if not stage_values:
            continue
        
        # Use final portion of stage for "converged" performance
        if len(stage_values) > stage_window:
            final_values = stage_values[-stage_window:]
        else:
            final_values = stage_values
        
        if final_values:
            stage_performance[stage_num] = {
                'mean': np.mean(final_values),
                'std': np.std(final_values),
                'min': np.min(final_values),
                'max': np.max(final_values),
                'n_steps': len(stage_values),
                'step_range': (start_step, end_step)
            }
    
    return stage_performance

def bootstrap_ci(data: List[float], n_bootstrap: int = 1000, confidence: float = 0.95) -> Tuple[float, float]:
    """Calculate bootstrap confidence interval"""
    if len(data) < 2:
        return (0.0, 0.0)
    
    bootstrap_means = []
    for _ in range(n_bootstrap):
        sample = np.random.choice(data, size=len(data), replace=True)
        bootstrap_means.append(np.mean(sample))
    
    alpha = 1 - confidence
    lower = np.percentile(bootstrap_means, 100 * alpha/2)
    upper = np.percentile(bootstrap_means, 100 * (1 - alpha/2))
    return (lower, upper)

def analyze_curriculum_aware(run_dirs: List[str], stage_window: int) -> Dict:
    """Analyze performance accounting for curriculum stages"""
    
    # Key metrics to analyze
    metrics = {
        'Drone__Success': {'name': 'Success Rate', 'format': 'percentage'},
        'Drone__Collisions': {'name': 'Collision Rate', 'format': 'percentage'},
        'Environment__Cumulative_Reward': {'name': 'Cumulative Reward', 'format': 'float'},
        'Drone__EpisodeLength': {'name': 'Episode Length', 'format': 'float'},
        'Drone__MinDistance': {'name': 'Min Distance to Goal', 'format': 'float'}
    }
    
    results = {}
    
    # First, find curriculum stage transitions
    all_stage_ranges = []
    for run_dir in run_dirs:
        stage_csv = os.path.join(run_dir, "Stage__Current.csv")
        if os.path.exists(stage_csv):
            stage_steps, stage_values = load_csv_data(stage_csv)
            if stage_steps and stage_values:
                stage_ranges = find_stage_transitions(stage_steps, stage_values)
                all_stage_ranges.append(stage_ranges)
                break  # Assume all runs have similar stage progression
    
    if not all_stage_ranges:
        print("Warning: No curriculum stage data found. Using final-window analysis.")
        # Fallback to final window analysis
        return analyze_final_performance_only(run_dirs, stage_window, metrics)
    
    stage_ranges = all_stage_ranges[0]  # Use first run's stage structure
    print(f"Found curriculum stages: {sorted(stage_ranges.keys())}")
    
    # Analyze each metric across curriculum stages
    for metric_key, metric_info in metrics.items():
        metric_name = metric_info['name']
        
        # Collect performance per stage across all runs
        stage_performances = {}  # stage_num -> list of performance values across runs
        
        for run_dir in run_dirs:
            csv_path = os.path.join(run_dir, f"{metric_key}.csv")
            if os.path.exists(csv_path):
                steps, values = load_csv_data(csv_path)
                if steps and values:
                    run_stage_perf = get_stage_performance(steps, values, stage_ranges, stage_window)
                    
                    for stage_num, perf_data in run_stage_perf.items():
                        if stage_num not in stage_performances:
                            stage_performances[stage_num] = []
                        stage_performances[stage_num].append(perf_data['mean'])
        
        # Calculate statistics for each stage
        results[metric_name] = {}
        for stage_num in sorted(stage_performances.keys()):
            stage_values = stage_performances[stage_num]
            
            if stage_values:
                mean_val = np.mean(stage_values)
                std_val = np.std(stage_values) if len(stage_values) > 1 else 0.0
                
                # Bootstrap CI
                if len(stage_values) > 1:
                    ci_lower, ci_upper = bootstrap_ci(stage_values)
                else:
                    # For single run, use the stage's internal variability
                    run_dir = run_dirs[0]
                    csv_path = os.path.join(run_dir, f"{metric_key}.csv")
                    steps, values = load_csv_data(csv_path)
                    if steps and values:
                        run_stage_perf = get_stage_performance(steps, values, stage_ranges, stage_window)
                        if stage_num in run_stage_perf:
                            # Use all values from this stage for CI
                            stage_start, stage_end = stage_ranges[stage_num]
                            stage_vals = [v for s, v in zip(steps, values) if stage_start <= s <= stage_end]
                            if len(stage_vals) > stage_window:
                                stage_vals = stage_vals[-stage_window:]
                            ci_lower, ci_upper = bootstrap_ci(stage_vals)
                        else:
                            ci_lower, ci_upper = (0.0, 0.0)
                    else:
                        ci_lower, ci_upper = (0.0, 0.0)
                
                results[metric_name][stage_num] = {
                    'mean': mean_val,
                    'std': std_val,
                    'ci_lower': ci_lower,
                    'ci_upper': ci_upper,
                    'format': metric_info['format'],
                    'n_runs': len(stage_values),
                    'step_range': stage_ranges[stage_num]
                }
    
    return results

def analyze_final_performance_only(run_dirs: List[str], final_window: int, metrics: Dict) -> Dict:
    """Fallback analysis using final performance only"""
    results = {}
    
    for metric_key, metric_info in metrics.items():
        metric_name = metric_info['name']
        all_final_values = []
        
        for run_dir in run_dirs:
            csv_path = os.path.join(run_dir, f"{metric_key}.csv")
            if os.path.exists(csv_path):
                steps, values = load_csv_data(csv_path)
                if values:
                    final_vals = values[-final_window:] if len(values) >= final_window else values
                    all_final_values.extend(final_vals)
        
        if all_final_values:
            mean_val = np.mean(all_final_values)
            std_val = np.std(all_final_values)
            ci_lower, ci_upper = bootstrap_ci(all_final_values)
            
            results[metric_name] = {
                'mean': mean_val,
                'std': std_val,
                'ci_lower': ci_lower,
                'ci_upper': ci_upper,
                'format': metric_info['format'],
                'n_runs': len(run_dirs)
            }
    
    return results

def format_value(value: float, format_type: str) -> str:
    """Format value according to type"""
    if format_type == 'percentage':
        return f"{value*100:.1f}%"
    elif format_type == 'float':
        if abs(value) > 100:
            return f"{value:.0f}"
        else:
            return f"{value:.2f}"
    return f"{value:.3f}"

def get_stage_description(stage_num: int) -> str:
    """Get human-readable stage description"""
    stage_descriptions = {
        0: "Basic Hover",
        1: "Simple Navigation", 
        2: "Obstacle Avoidance",
        3: "Urban Environment",
        4: "Adverse Weather",
        5: "Emergency Scenarios"
    }
    return stage_descriptions.get(stage_num, f"Stage {stage_num}")

def generate_curriculum_summary(results: Dict, output_file: str, is_curriculum_aware: bool = True):
    """Generate curriculum-aware summary tables"""
    
    # Text table
    with open(output_file, 'w') as f:
        f.write("CURRICULUM-AWARE STATISTICAL SUMMARY\n")
        f.write("=" * 100 + "\n\n")
        
        if is_curriculum_aware:
            f.write("Performance metrics analyzed per curriculum stage\n")
            f.write("(Accounts for increasing environment difficulty)\n\n")
            
            # Per-stage analysis
            if results:
                # Find all stages across metrics
                all_stages = set()
                for metric_data in results.values():
                    if isinstance(metric_data, dict):
                        all_stages.update(metric_data.keys())
                
                for stage_num in sorted(all_stages):
                    f.write(f"STAGE {stage_num}: {get_stage_description(stage_num)}\n")
                    f.write("-" * 80 + "\n")
                    f.write(f"{'Metric':<25} {'Mean ± Std':<20} {'95% CI':<25} {'N':<5}\n")
                    f.write("-" * 80 + "\n")
                    
                    for metric_name, metric_data in results.items():
                        if stage_num in metric_data:
                            data = metric_data[stage_num]
                            mean_str = format_value(data['mean'], data['format'])
                            std_str = format_value(data['std'], data['format'])
                            ci_lower_str = format_value(data['ci_lower'], data['format'])
                            ci_upper_str = format_value(data['ci_upper'], data['format'])
                            
                            mean_std = f"{mean_str} ± {std_str}"
                            ci_range = f"[{ci_lower_str}, {ci_upper_str}]"
                            
                            f.write(f"{metric_name:<25} {mean_std:<20} {ci_range:<25} {data['n_runs']:<5}\n")
                    
                    f.write("-" * 80 + "\n\n")
            
            # Final stage summary (for dissertation table)
            f.write("FINAL PERFORMANCE SUMMARY (Highest Stage Achieved)\n")
            f.write("=" * 80 + "\n")
            
            if results:
                # Find highest stage for each metric
                final_stage_data = {}
                for metric_name, metric_data in results.items():
                    if isinstance(metric_data, dict) and metric_data:
                        highest_stage = max(metric_data.keys())
                        final_stage_data[metric_name] = metric_data[highest_stage]
                
                f.write(f"{'Metric':<25} {'Mean ± Std':<20} {'95% CI':<25} {'Stage':<8}\n")
                f.write("-" * 80 + "\n")
                
                for metric_name, data in final_stage_data.items():
                    mean_str = format_value(data['mean'], data['format'])
                    std_str = format_value(data['std'], data['format'])
                    ci_lower_str = format_value(data['ci_lower'], data['format'])
                    ci_upper_str = format_value(data['ci_upper'], data['format'])
                    
                    mean_std = f"{mean_str} ± {std_str}"
                    ci_range = f"[{ci_lower_str}, {ci_upper_str}]"
                    
                    # Find which stage this data came from
                    stage_num = None
                    for s_num, s_data in results[metric_name].items():
                        if s_data == data:
                            stage_num = s_num
                            break
                    
                    f.write(f"{metric_name:<25} {mean_std:<20} {ci_range:<25} {stage_num:<8}\n")
                
                f.write("-" * 80 + "\n")
        else:
            f.write("Final performance analysis (curriculum stages not detected)\n\n")
            
            f.write(f"{'Metric':<25} {'Mean ± Std':<20} {'95% CI':<25} {'N':<5}\n")
            f.write("-" * 80 + "\n")
            
            for metric_name, data in results.items():
                mean_str = format_value(data['mean'], data['format'])
                std_str = format_value(data['std'], data['format'])
                ci_lower_str = format_value(data['ci_lower'], data['format'])
                ci_upper_str = format_value(data['ci_upper'], data['format'])
                
                mean_std = f"{mean_str} ± {std_str}"
                ci_range = f"[{ci_lower_str}, {ci_upper_str}]"
                
                f.write(f"{metric_name:<25} {mean_std:<20} {ci_range:<25} {data['n_runs']:<5}\n")
            
            f.write("-" * 80 + "\n")

    # LaTeX table for final performance
    latex_file = output_file.replace('.txt', '_latex.txt')
    with open(latex_file, 'w') as f:
        f.write("% Curriculum-aware LaTeX table for dissertation\n")
        f.write("\\begin{table}[htbp]\n")
        f.write("\\centering\n")
        
        if is_curriculum_aware:
            f.write("\\caption{Final Performance Metrics by Highest Curriculum Stage Achieved}\n")
            f.write("\\label{tab:curriculum_final_performance}\n")
            f.write("\\begin{tabular}{lcccc}\n")
            f.write("\\toprule\n")
            f.write("Metric & Mean ± Std & 95\\% CI & Stage & Description \\\\\n")
            f.write("\\midrule\n")
            
            if results:
                for metric_name, metric_data in results.items():
                    if isinstance(metric_data, dict) and metric_data:
                        highest_stage = max(metric_data.keys())
                        data = metric_data[highest_stage]
                        
                        mean_str = format_value(data['mean'], data['format'])
                        std_str = format_value(data['std'], data['format'])
                        ci_lower_str = format_value(data['ci_lower'], data['format'])
                        ci_upper_str = format_value(data['ci_upper'], data['format'])
                        stage_desc = get_stage_description(highest_stage)
                        
                        f.write(f"{metric_name} & {mean_str} ± {std_str} & [{ci_lower_str}, {ci_upper_str}] & {highest_stage} & {stage_desc} \\\\\n")
        else:
            f.write("\\caption{Final Performance Metrics}\n")
            f.write("\\label{tab:final_performance}\n")
            f.write("\\begin{tabular}{lccc}\n")
            f.write("\\toprule\n")
            f.write("Metric & Mean ± Std & 95\\% CI & N \\\\\n")
            f.write("\\midrule\n")
            
            for metric_name, data in results.items():
                mean_str = format_value(data['mean'], data['format'])
                std_str = format_value(data['std'], data['format'])
                ci_lower_str = format_value(data['ci_lower'], data['format'])
                ci_upper_str = format_value(data['ci_upper'], data['format'])
                
                f.write(f"{metric_name} & {mean_str} ± {std_str} & [{ci_lower_str}, {ci_upper_str}] & {data['n_runs']} \\\\\n")
        
        f.write("\\bottomrule\n")
        f.write("\\end{tabular}\n")
        f.write("\\end{table}\n\n")
        
        f.write("% Note: Performance measured at convergence within each curriculum stage\n")
        f.write("% CI calculated using bootstrap sampling to account for training variability\n")

def main():
    args = parse_args()
    
    print(f"Analyzing {len(args.run_dirs)} run directory(ies) with curriculum awareness...")
    
    # Check directories
    valid_dirs = []
    for run_dir in args.run_dirs:
        if os.path.exists(run_dir):
            csv_files = glob.glob(os.path.join(run_dir, "*.csv"))
            if csv_files:
                valid_dirs.append(run_dir)
                print(f"Found {len(csv_files)} CSV files in {run_dir}")
            else:
                print(f"No CSV files found in {run_dir}")
        else:
            print(f"Directory not found: {run_dir}")
    
    if not valid_dirs:
        print("ERROR: No valid directories found.")
        return
    
    # Analyze with curriculum awareness
    results = analyze_curriculum_aware(valid_dirs, args.stage_window)
    
    if not results:
        print("ERROR: No metrics could be analyzed.")
        return
    
    # Check if curriculum-aware analysis was successful
    is_curriculum_aware = any(isinstance(data, dict) and len(data) > 1 for data in results.values())
    
    # Generate summary
    generate_curriculum_summary(results, args.out_file, is_curriculum_aware)
    
    print(f"\nCurriculum-aware statistical summary generated:")
    print(f"   Text summary: {args.out_file}")
    print(f"   LaTeX table: {args.out_file.replace('.txt', '_latex.txt')}")
    
    if is_curriculum_aware:
        print(f"\nCurriculum-aware analysis completed!")
        print(f"   Performance measured per curriculum stage")
        print(f"   Accounts for increasing environment difficulty")
    else:
        print(f"\nFinal performance analysis (curriculum stages not detected)")

if __name__ == "__main__":
    main()

