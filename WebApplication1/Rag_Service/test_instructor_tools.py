"""
test_instructor_tools.py
Tests the Instructor Agentic Tools:
1. Real-time metrics reporting (student count, grading backlog)
2. Intent classification
3. Targeted assignment retrieval
4. AI Auto-Evaluation with Gemini
"""

import sys
import os

sys.path.append(os.path.dirname(__file__))
from instructor_tools import (
    detect_instructor_intent,
    get_instructor_metrics_report,
    get_target_submission,
    evaluate_submission_ai
)

print("=" * 70)
print("TEST 1: Intent Detection")
print("=" * 70)
intent1, params1 = detect_instructor_intent("How many students are enrolled and how many assignments do I have to evaluate?")
print(f"Query 1 Intent: {intent1} | Params: {params1}")
assert intent1 == "metrics", f"Expected 'metrics', got {intent1}"

intent2, params2 = detect_instructor_intent("Can you evaluate Rahul's assignment for Lesson 2?")
print(f"Query 2 Intent: {intent2} | Params: {params2}")
assert intent2 == "evaluate", f"Expected 'evaluate', got {intent2}"
assert params2.get("target_student") == "rahul"

print("\n" + "=" * 70)
print("TEST 2: Live Database Metrics Report (Instructor 1 - A. Sharma)")
print("=" * 70)
report = get_instructor_metrics_report(1)
print(report)
assert "Total Enrolled Students" in report
assert "Assignments Waiting for Evaluation" in report

print("\n" + "=" * 70)
print("TEST 3: Target Submission Retrieval (Rahul Verma)")
print("=" * 70)
sub = get_target_submission(1, {"target_student": "rahul"})
assert sub is not None, "Failed to retrieve Rahul's submission"
print(f"Retrieved Submission #{sub['submission_id']} by {sub['student_name']}")
print(f"Task: {sub['submission_title']}")
print(f"Status: {sub['status']}")

print("\n" + "=" * 70)
print("TEST 4: AI Auto-Evaluation with Gemini")
print("=" * 70)
eval_result = evaluate_submission_ai(sub)
print(eval_result)
assert "Recommended Score" in eval_result or "Score" in eval_result
print("\nALL INSTRUCTOR AGENTIC TOOL TESTS PASSED!")
