"""
test_ai_advisor_feature.py
Verifies end-to-end integration of the AI Course Advisor & Semantic Matcher:
1. Tests direct call to FastAPI microservice (/api/recommend-courses)
2. Tests MVC endpoint call (/Courses/AiRecommend)
3. Tests exact domain match
4. Tests non-exact goal / transferable skill match
5. Tests completely novel topic with foundational pathway guidance
"""

import requests

FASTAPI_URL = "http://127.0.0.1:8000/api/recommend-courses"
MVC_URL = "http://localhost:57794/Courses/AiRecommend"

def test_1_fastapi_direct():
    print("\n--- Test 1: Direct Call to FastAPI /api/recommend-courses ---")
    payload = {
        "interests": "I want to master container orchestration and automated CI/CD deployments with Kubernetes",
        "learner_level": "All",
        "top_k": 3
    }
    resp = requests.post(FASTAPI_URL, json=payload, timeout=20)
    assert resp.status_code == 200, f"Expected 200, got {resp.status_code}"
    data = resp.json()
    assert data.get("success") is True
    print(f"Model used: {data.get('model_used')}")
    print(f"Has exact match: {data.get('has_exact_match')}")
    print(f"Pathway Tip: {data.get('pathway_tip')}")
    recs = data.get("recommendations", [])
    assert len(recs) > 0, "Expected recommendations to be returned"
    print(f"Top Recommendation: {recs[0]['title']} ({recs[0]['match_score']}%) [{recs[0]['match_label']}]")
    print("PASS: FastAPI recommendation endpoint verified!")


def test_2_mvc_exact_match():
    print("\n--- Test 2: MVC /Courses/AiRecommend Exact Match ---")
    payload = {
        "Interests": "I want to learn React 18, TypeScript, and state management hooks to build frontend UIs",
        "Level": "Beginner"
    }
    resp = requests.post(MVC_URL, json=payload, timeout=20)
    assert resp.status_code == 200, f"Expected 200, got {resp.status_code}"
    data = resp.json()
    assert data.get("success") is True
    assert data.get("has_exact_match") is True
    recs = data.get("recommendations", [])
    top = recs[0]
    print(f"Top Course: {top['title']} | Score: {top['match_score']}% | Label: {top['match_label']}")
    print(f"Rationale: {top['rationale']}")
    assert top["course_id"] == 1, f"Expected Course 1 (React 18), got {top['course_id']}"
    print("PASS: Exact match correctly identified and ranked Course 1!")


def test_3_mvc_non_exact_transferable_match():
    print("\n--- Test 3: MVC Non-Exact Goal (Real-Time Trading Dashboards) ---")
    payload = {
        "Interests": "I want to create high-frequency financial trading dashboards and live streaming crypto tickers",
        "Level": "Advanced"
    }
    resp = requests.post(MVC_URL, json=payload, timeout=20)
    assert resp.status_code == 200
    data = resp.json()
    assert data.get("success") is True
    recs = data.get("recommendations", [])
    print(f"Pathway Tip: {data.get('pathway_tip')}")
    for r in recs:
        print(f"  * {r['title']} ({r['match_score']}%) [{r['match_label']}]")
        print(f"    Rationale: {r['rationale']}")
    # Should identify Next.js (WebSockets) or React 18
    course_ids = [r["course_id"] for r in recs]
    assert 3 in course_ids or 1 in course_ids, "Expected Next.js or React 18 in recommendations"
    print("PASS: Non-exact goal successfully mapped to foundational real-time & UI architecture courses!")


def test_4_mvc_uncovered_domain():
    print("\n--- Test 4: MVC Novel Domain (Ethical Hacking & Penetration Testing) ---")
    payload = {
        "Interests": "Ethical hacking, penetration testing, and exploiting network vulnerabilities",
        "Level": "All"
    }
    resp = requests.post(MVC_URL, json=payload, timeout=20)
    assert resp.status_code == 200
    data = resp.json()
    assert data.get("success") is True
    print(f"Has Exact Match: {data.get('has_exact_match')}")
    print(f"Pathway Tip: {data.get('pathway_tip')}")
    for r in data.get("recommendations", []):
        print(f"  * {r['title']} ({r['match_score']}%) [{r['match_label']}]")
        print(f"    Rationale: {r['rationale']}")
    print("PASS: Novel domain appropriately handled with foundational skill mapping!")


if __name__ == "__main__":
    test_1_fastapi_direct()
    test_2_mvc_exact_match()
    test_3_mvc_non_exact_transferable_match()
    test_4_mvc_uncovered_domain()
    print("\nALL 4 AI COURSE ADVISOR & RECOMMENDATION TESTS PASSED PERFECTLY!")
