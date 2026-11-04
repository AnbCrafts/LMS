"""
test_recommender.py
Tests the AI Course Recommendation Engine across 3 distinct student scenarios:
1. Exact match query
2. Non-exact / creative goal query (e.g., real-time trading dashboards & chat)
3. Specialized cloud DevOps query
"""

import sys
import os

sys.path.append(os.path.dirname(__file__))
from course_recommender import recommender

print("=" * 70)
print("TEST 1: Non-exact goal query (Real-time Trading & Chat apps)")
print("=" * 70)
res1 = recommender.recommend("I want to build real-time trading dashboards and live chat servers")
print(f"Success: {res1.get('success')}")
print(f"Has Exact Match: {res1.get('has_exact_match')}")
print(f"Pathway Tip: {res1.get('pathway_tip')}")
print("Recommended Courses:")
for r in res1.get("recommendations", []):
    print(f"  * [{r['match_score']}% | {r['match_label']}] {r['title']}")
    print(f"    Rationale: {r['rationale']}")

print("\n" + "=" * 70)
print("TEST 2: Direct React & UI Frontend interest")
print("=" * 70)
res2 = recommender.recommend("I want to build modern reactive user interfaces with TypeScript and custom hooks")
for r in res2.get("recommendations", []):
    print(f"  * [{r['match_score']}% | {r['match_label']}] {r['title']}")
    print(f"    Rationale: {r['rationale']}")

print("\n" + "=" * 70)
print("TEST 3: Completely novel domain (Cyber Security & Ethical Hacking)")
print("=" * 70)
res3 = recommender.recommend("I want to learn ethical hacking, network penetration testing, and security defenses")
print(f"Has Exact Match: {res3.get('has_exact_match')}")
print(f"Pathway Tip: {res3.get('pathway_tip')}")
for r in res3.get("recommendations", []):
    print(f"  * [{r['match_score']}% | {r['match_label']}] {r['title']}")
    print(f"    Rationale: {r['rationale']}")
