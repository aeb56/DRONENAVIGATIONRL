#!/usr/bin/env python3
"""
Export TensorBoard data to PNG plots for my dissertation.

Usage:
  python export_tensorboard_pngs_improved.py \
    --run_dir "results/curriculum_run_final/DroneAgent" \
    --out_dir "results/curriculum_run_final/plots_improved" \
    --smoothing 0.9

What this does:
- Makes nice looking plots from training data
- Formats percentages properly
- Smooths out noisy data
- Adds proper labels and titles
- Makes plots look professional
"""

import argparse
import os
import re
import sys
import math
import numpy as np
from typing import Dict, List, Tuple, Optional

try:
    from tensorboard.backend.event_processing.event_accumulator import EventAccumulator
except Exception as exc:
    print("ERROR: Failed to import tensorboard. Please install it:\n  pip install tensorboard")
    raise

try:
    import matplotlib
    matplotlib.use("Agg")  # headless
    import matplotlib.pyplot as plt
    from matplotlib.ticker import FuncFormatter
    import matplotlib.patches as patches
except Exception:
    print("ERROR: Failed to import matplotlib. Please install it:\n  pip install matplotlib")
    raise

# Make plots look nice
plt.style.use('seaborn-v0_8-whitegrid' if 'seaborn-v0_8-whitegrid' in plt.style.available else 'default')

def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Export TensorBoard scalars to publication-ready PNGs")
    parser.add_argument("--run_dir", required=True, help="Path to run dir containing events.out.tfevents.*")
    parser.add_argument("--out_dir", required=False, default=None, help="Output directory for PNGs")
    parser.add_argument("--smoothing", type=float, default=0.9, help="Exponential smoothing factor")
    parser.add_argument("--moving_avg", type=int, default=50, help="Moving average window for noisy plots")
    parser.add_argument("--dpi", type=int, default=300, help="Figure DPI for publication quality")
    parser.add_argument("--width", type=float, default=10.0, help="Figure width (inches)")
    parser.add_argument("--height", type=float, default=6.0, help="Figure height (inches)")
    parser.add_argument("--csv", action="store_true", help="Also write CSV files")
    return parser.parse_args()

def ensure_out_dir(path: str, run_dir: str) -> str:
    if path is None:
        path = os.path.join(os.path.dirname(run_dir), "plots_improved")
    os.makedirs(path, exist_ok=True)
    return path

def sanitize_filename(name: str) -> str:
    name = name.replace("/", "__").replace(" ", "_")
    return re.sub(r"[^A-Za-z0-9_.\-]+", "_", name)

def moving_average(values: List[float], window: int) -> List[float]:
    if window <= 1 or len(values) < window:
        return values[:]
    smoothed = []
    for i in range(len(values)):
        start = max(0, i - window + 1)
        end = i + 1
        smoothed.append(np.mean(values[start:end]))
    return smoothed

def exp_smooth(values: List[float], alpha: float) -> List[float]:
    if not values or alpha <= 0.0:
        return values[:]
    smoothed = []
    last = values[0]
    smoothed.append(last)
    for v in values[1:]:
        last = alpha * last + (1.0 - alpha) * v
        smoothed.append(last)
    return smoothed

def load_scalars(run_dir: str) -> Dict[str, Tuple[List[int], List[float]]]:
    size_guidance = {
        'scalars': 0, 'histograms': 0, 'images': 0, 'audio': 0,
        'compressedHistograms': 0, 'tensors': 0,
    }
    acc = EventAccumulator(run_dir, size_guidance=size_guidance)
    acc.Reload()
    tags = acc.Tags().get('scalars', [])
    data: Dict[str, Tuple[List[int], List[float]]] = {}
    for tag in tags:
        events = acc.Scalars(tag)
        steps = [e.step for e in events]
        vals = [float(e.value) for e in events]
        data[tag] = (steps, vals)
    return data

def format_steps_axis(x, pos):
    """Show steps in millions"""
    return f'{x/1e6:.1f}M'

def format_percentage_axis(x, pos):
    """Show as percentage"""
    return f'{x*100:.0f}%'

def get_plot_config(tag: str) -> Dict:
    """Get plot-specific configuration"""
    configs = {
        'Drone/Success': {
            'title': 'Success Rate',
            'ylabel': 'Success Rate (%)',
            'ylim': (0, 1),
            'format_y': 'percentage',
            'color': '#2E8B57',  # Sea green
            'use_moving_avg': True,
            'final_marker': True
        },
        'Drone/Collisions': {
            'title': 'Collision Rate',
            'ylabel': 'Collision Rate (%)',
            'ylim': (0, None),
            'format_y': 'percentage',
            'color': '#DC143C',  # Crimson
            'use_moving_avg': True,
            'final_marker': True
        },
        'Environment/Cumulative Reward': {
            'title': 'Cumulative Reward',
            'ylabel': 'Cumulative Reward',
            'color': '#4169E1',  # Royal blue
            'use_moving_avg': True,
            'final_marker': True
        },
        'Drone/EpisodeLength': {
            'title': 'Episode Length',
            'ylabel': 'Episode Length (steps)',
            'color': '#FF8C00',  # Dark orange
            'use_moving_avg': True
        },
        'Drone/MinDistance': {
            'title': 'Minimum Distance to Goal',
            'ylabel': 'Min Distance (m)',
            'color': '#9932CC',  # Dark orchid
            'use_moving_avg': True
        },
        'Policy/Extrinsic Reward': {
            'title': 'Policy Reward',
            'ylabel': 'Reward',
            'color': '#4169E1',
            'use_moving_avg': True
        },
        'Losses/Policy Loss': {
            'title': 'Policy Loss',
            'ylabel': 'Loss',
            'color': '#B22222',  # Fire brick
            'use_moving_avg': True
        },
        'Losses/Value Loss': {
            'title': 'Value Loss',
            'ylabel': 'Loss',
            'color': '#8B0000',  # Dark red
            'use_moving_avg': True
        },
        'Policy/Entropy': {
            'title': 'Policy Entropy',
            'ylabel': 'Entropy',
            'color': '#008B8B',  # Dark cyan
            'use_moving_avg': True
        },
        'Environment/Lesson Number/stage': {
            'title': 'Curriculum Stage',
            'ylabel': 'Stage Number',
            'color': '#9370DB',  # Medium purple
            'ylim': (0, 10),
            'use_moving_avg': False
        }
    }
    
    # Default config
    default = {
        'title': tag.replace('/', ' ').replace('_', ' ').title(),
        'ylabel': 'Value',
        'color': '#1f77b4',  # Default matplotlib blue
        'use_moving_avg': False,
        'format_y': None,
        'ylim': None,
        'final_marker': False
    }
    
    return configs.get(tag, default)

def plot_series_improved(tag: str, steps: List[int], values: List[float], out_png: str,
                        width: float, height: float, dpi: int, moving_avg_window: int) -> None:
    config = get_plot_config(tag)
    
    # Apply smoothing
    if config['use_moving_avg'] and len(values) > moving_avg_window:
        smoothed_values = moving_average(values, moving_avg_window)
    else:
        smoothed_values = values[:]
    
    # Create figure with professional styling
    fig, ax = plt.subplots(figsize=(width, height), dpi=dpi)
    
    # Plot main line
    ax.plot(steps, smoothed_values, color=config['color'], linewidth=2.5, alpha=0.8)
    
    # Add final performance marker
    if config.get('final_marker', False) and values:
        final_val = smoothed_values[-1]
        ax.axhline(y=final_val, color=config['color'], linestyle='--', alpha=0.6, linewidth=1.5)
        ax.text(0.02, 0.98, f'Final: {final_val:.1%}' if config.get('format_y') == 'percentage' else f'Final: {final_val:.2f}',
                transform=ax.transAxes, fontsize=11, verticalalignment='top',
                bbox=dict(boxstyle='round,pad=0.3', facecolor='white', alpha=0.8))
    
    # Set labels and title
    ax.set_xlabel('Training Steps', fontsize=14, fontweight='bold')
    ax.set_ylabel(config['ylabel'], fontsize=14, fontweight='bold')
    ax.set_title(config['title'], fontsize=16, fontweight='bold', pad=20)
    
    # Format axes
    ax.xaxis.set_major_formatter(FuncFormatter(format_steps_axis))
    if config.get('format_y') == 'percentage':
        ax.yaxis.set_major_formatter(FuncFormatter(format_percentage_axis))
    
    # Set y-limits if specified
    if config.get('ylim'):
        ax.set_ylim(config['ylim'])
    
    # Styling
    ax.grid(True, alpha=0.3, linestyle='-', linewidth=0.5)
    ax.spines['top'].set_visible(False)
    ax.spines['right'].set_visible(False)
    ax.spines['left'].set_linewidth(1.5)
    ax.spines['bottom'].set_linewidth(1.5)
    
    # Tick formatting
    ax.tick_params(axis='both', which='major', labelsize=12, width=1.5)
    ax.tick_params(axis='both', which='minor', width=1)
    
    plt.tight_layout()
    plt.savefig(out_png, dpi=dpi, bbox_inches='tight', facecolor='white')
    plt.close()

def create_results_overview(selected_data: List[Tuple[str, List[int], List[float]]], 
                           out_png: str, dpi: int, moving_avg_window: int) -> None:
    """Create a 2x2 overview of key results metrics"""
    if len(selected_data) < 4:
        return
    
    fig, axes = plt.subplots(2, 2, figsize=(15, 10), dpi=dpi)
    axes = axes.flatten()
    
    # Priority order for overview
    priority_tags = ['Drone/Success', 'Drone/Collisions', 'Environment/Cumulative Reward', 'Drone/EpisodeLength']
    
    # Match available data to priority tags
    plot_data = []
    for priority_tag in priority_tags:
        for tag, steps, vals in selected_data:
            if tag == priority_tag:
                plot_data.append((tag, steps, vals))
                break
    
    # Fill remaining slots if needed
    while len(plot_data) < 4 and len(plot_data) < len(selected_data):
        for tag, steps, vals in selected_data:
            if not any(existing_tag == tag for existing_tag, _, _ in plot_data):
                plot_data.append((tag, steps, vals))
                break
    
    for idx, (tag, steps, vals) in enumerate(plot_data[:4]):
        ax = axes[idx]
        config = get_plot_config(tag)
        
        # Apply smoothing
        if config['use_moving_avg'] and len(vals) > moving_avg_window:
            smoothed_vals = moving_average(vals, moving_avg_window)
        else:
            smoothed_vals = vals[:]
        
        # Plot
        ax.plot(steps, smoothed_vals, color=config['color'], linewidth=2, alpha=0.8)
        
        # Final value marker
        if config.get('final_marker', False) and vals:
            final_val = smoothed_vals[-1]
            ax.axhline(y=final_val, color=config['color'], linestyle='--', alpha=0.5, linewidth=1)
        
        # Formatting
        ax.set_title(config['title'], fontsize=14, fontweight='bold')
        ax.set_xlabel('Training Steps', fontsize=12)
        ax.set_ylabel(config['ylabel'], fontsize=12)
        ax.xaxis.set_major_formatter(FuncFormatter(format_steps_axis))
        
        if config.get('format_y') == 'percentage':
            ax.yaxis.set_major_formatter(FuncFormatter(format_percentage_axis))
        
        if config.get('ylim'):
            ax.set_ylim(config['ylim'])
        
        ax.grid(True, alpha=0.3)
        ax.spines['top'].set_visible(False)
        ax.spines['right'].set_visible(False)
    
    plt.suptitle('Training Results Overview', fontsize=18, fontweight='bold', y=0.98)
    plt.tight_layout()
    plt.savefig(out_png, dpi=dpi, bbox_inches='tight', facecolor='white')
    plt.close()

def save_csv(path: str, steps: List[int], values: List[float]) -> None:
    with open(path, 'w', encoding='utf-8') as f:
        f.write("step,value\n")
        for s, v in zip(steps, values):
            f.write(f"{s},{v}\n")

def main(args: argparse.Namespace) -> None:
    run_dir = os.path.abspath(args.run_dir)
    if not os.path.isdir(run_dir):
        print(f"ERROR: run_dir does not exist: {run_dir}")
        sys.exit(1)

    out_dir = ensure_out_dir(args.out_dir, run_dir)
    print(f"Reading TensorBoard scalars from: {run_dir}")
    print(f"Writing improved PNGs to: {out_dir}")

    data = load_scalars(run_dir)
    if not data:
        print("No scalar tags found.")
        sys.exit(2)

    # Write tag listing
    tag_list_path = os.path.join(out_dir, "available_tags.txt")
    with open(tag_list_path, 'w', encoding='utf-8') as f:
        for tag in sorted(data.keys()):
            f.write(tag + "\n")
    print(f"Found {len(data)} scalar tags (listed in {tag_list_path}).")

    # Key metrics for overview
    key_metrics = ['Drone/Success', 'Drone/Collisions', 'Environment/Cumulative Reward', 'Drone/EpisodeLength']
    overview_data = []

    # Export each tag with improvements
    for tag, (steps, vals) in data.items():
        if not steps:
            continue
        
        # Apply exponential smoothing first
        smoothed = exp_smooth(vals, args.smoothing)
        
        # Create improved plot
        safe = sanitize_filename(tag)
        out_png = os.path.join(out_dir, f"{safe}.png")
        plot_series_improved(tag, steps, smoothed, out_png, args.width, args.height, args.dpi, args.moving_avg)
        
        # Save CSV if requested
        if args.csv:
            out_csv = os.path.join(out_dir, f"{safe}.csv")
            save_csv(out_csv, steps, smoothed)
        
        # Collect for overview
        if tag in key_metrics:
            overview_data.append((tag, steps, smoothed))

    # Create results overview
    if overview_data:
        overview_png = os.path.join(out_dir, "results_overview.png")
        create_results_overview(overview_data, overview_png, args.dpi, args.moving_avg)
        print(f"Created results overview: {overview_png}")

    print("Export complete with publication-ready formatting!")
    print(f"Key plots for dissertation:")
    print(f"   - results_overview.png (main figure)")
    print(f"   - Drone__Success.png (primary metric)")
    print(f"   - Drone__Collisions.png (safety metric)")
    print(f"   - Environment__Cumulative_Reward.png (learning curve)")

if __name__ == "__main__":
    args = parse_args()
    main(args)

