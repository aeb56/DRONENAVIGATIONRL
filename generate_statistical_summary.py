#!/usr/bin/env python3
"""
Generate statistical summary table from training run(s) for dissertation.
Handles both single-run analysis and multi-seed analysis if available.
"""

import argparse
import os
import pandas as pd
import numpy as np
from typing import Dict, List, Tuple
import glob

def parse_args():
    parser = argparse.ArgumentParser(description="Generate statistical summary table")
    parser.add_argument("--run_dirs", nargs='+', required=True, 
                       help="Path(s) to run directories with CSV files")
    parser.add_argument("--out_file", default="statistical_summary.txt", 
                       help="Output file for summary table")
    parser.add_argument("--final_window", type=int, default=100,
                       help="Number of final steps to average for 'final performance'")
    return parser.parse_args()

def load_csv_data(csv_path: str) -> Tuple[List[int], List[float]]:
    """Load step,value data from CSV"""
    try:
        df = pd.read_csv(csv_path)
        return df['step'].tolist(), df['value'].tolist()
    except Exception as e:
        print(f"Warning: Could not load {csv_path}: {e}")
        return [], []

def get_final_performance(values: List[float], window: int) -> Dict[str, float]:
    """Calculate final performance statistics"""
    if not values or len(values) < window:
        final_vals = values
    else:
        final_vals = values[-window:]
    
    if not final_vals:
        return {'mean': 0.0, 'std': 0.0, 'min': 0.0, 'max': 0.0}
    
    return {
        'mean': np.mean(final_vals),
        'std': np.std(final_vals),
        'min': np.min(final_vals),
        'max': np.max(final_vals)
    }

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

def analyze_runs(run_dirs: List[str], final_window: int) -> Dict:
    """Analyze one or more training runs"""
    
    # Key metrics to analyze
    metrics = {
        'Drone__Success': {'name': 'Success Rate', 'format': 'percentage'},
        'Drone__Collisions': {'name': 'Collision Rate', 'format': 'percentage'},
        'Environment__Cumulative_Reward': {'name': 'Cumulative Reward', 'format': 'float'},
        'Drone__EpisodeLength': {'name': 'Episode Length', 'format': 'float'},
        'Drone__MinDistance': {'name': 'Min Distance to Goal', 'format': 'float'}
    }
    
    results = {}
    
    for metric_key, metric_info in metrics.items():
        metric_name = metric_info['name']
        all_final_values = []
        
        for run_dir in run_dirs:
            csv_path = os.path.join(run_dir, f"{metric_key}.csv")
            if os.path.exists(csv_path):
                steps, values = load_csv_data(csv_path)
                if values:
                    final_stats = get_final_performance(values, final_window)
                    all_final_values.append(final_stats['mean'])
        
        if all_final_values:
            # Calculate statistics across runs (or single run)
            mean_val = np.mean(all_final_values)
            std_val = np.std(all_final_values) if len(all_final_values) > 1 else 0.0
            
            # Bootstrap CI
            if len(all_final_values) > 1:
                ci_lower, ci_upper = bootstrap_ci(all_final_values)
            else:
                # For single run, use the final window for CI
                run_dir = run_dirs[0]
                csv_path = os.path.join(run_dir, f"{metric_key}.csv")
                _, values = load_csv_data(csv_path)
                if values:
                    final_vals = values[-final_window:] if len(values) >= final_window else values
                    ci_lower, ci_upper = bootstrap_ci(final_vals)
                else:
                    ci_lower, ci_upper = (0.0, 0.0)
            
            results[metric_name] = {
                'mean': mean_val,
                'std': std_val,
                'ci_lower': ci_lower,
                'ci_upper': ci_upper,
                'format': metric_info['format'],
                'n_runs': len(all_final_values)
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

def generate_summary_table(results: Dict, output_file: str):
    """Generate LaTeX and text summary tables"""
    
    # Text table
    with open(output_file, 'w') as f:
        f.write("STATISTICAL SUMMARY TABLE\n")
        f.write("=" * 80 + "\n\n")
        
        if results:
            n_runs = list(results.values())[0]['n_runs']
            if n_runs == 1:
                f.write("Single-run analysis with bootstrap confidence intervals\n")
                f.write(f"(Final {100} steps used for bootstrap sampling)\n\n")
            else:
                f.write(f"Multi-run analysis (n={n_runs} seeds)\n\n")
        
        f.write("Table 4.1: Final Performance Metrics\n")
        f.write("-" * 80 + "\n")
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
        
        f.write("-" * 80 + "\n\n")
    
    # LaTeX table
    latex_file = output_file.replace('.txt', '_latex.txt')
    with open(latex_file, 'w') as f:
        f.write("% LaTeX table for dissertation\n")
        f.write("\\begin{table}[htbp]\n")
        f.write("\\centering\n")
        f.write("\\caption{Final Performance Metrics")
        if results:
            n_runs = list(results.values())[0]['n_runs']
            if n_runs == 1:
                f.write(" (Single Run with Bootstrap CI)")
            else:
                f.write(f" (n={n_runs} seeds)")
        f.write("}\n")
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
        
        # Add interpretation note
        f.write("% Note for dissertation:\n")
        if results and list(results.values())[0]['n_runs'] == 1:
            f.write("% Single-run results with bootstrap confidence intervals.\n")
            f.write("% CI calculated from final 100 training steps to show convergence stability.\n")
        else:
            f.write("% Multi-seed results showing mean ± standard deviation across independent runs.\n")

def main():
    args = parse_args()
    
    print(f"Analyzing {len(args.run_dirs)} run directory(ies)...")
    
    # Check if directories exist and contain CSV files
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
        print("ERROR: No valid directories with CSV files found.")
        return
    
    # Analyze runs
    results = analyze_runs(valid_dirs, args.final_window)
    
    if not results:
        print("ERROR: No metrics could be analyzed.")
        return
    
    # Generate summary
    generate_summary_table(results, args.out_file)
    
    print(f"\nStatistical summary generated:")
    print(f"   Text table: {args.out_file}")
    print(f"   LaTeX table: {args.out_file.replace('.txt', '_latex.txt')}")
    print(f"\nSummary:")
    
    for metric_name, data in results.items():
        mean_str = format_value(data['mean'], data['format'])
        print(f"   {metric_name}: {mean_str}")

if __name__ == "__main__":
    main()

