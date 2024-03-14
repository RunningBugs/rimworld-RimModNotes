#!/usr/bin/python3
"""
This module provides a function to remove duplicate lines from a file.
"""

def remove_duplicates(filename):
    """
    Removes duplicate lines from a file and saves the unique lines back to the same file.

    Args:
        filename (str): The path of the file to remove duplicates from.

    Returns:
        None
    """
    with open(filename, 'r', encoding='utf-8') as file:
        lines = file.readlines()
    unique_lines = []
    for line in lines:
        if line not in unique_lines:
            unique_lines.append(line)
    for line in unique_lines:
        print(line.strip())

# Replace 'yourfile.txt' with the path to your text file
if __name__ == '__main__':
    remove_duplicates('input.txt')
