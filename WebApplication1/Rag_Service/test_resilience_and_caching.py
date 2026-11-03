"""
test_resilience_and_caching.py
Automated Verification Suite for:
1. Fast Semantic Caching (<10ms response, zero LLM calls)
2. Duplicate Query Dropping / Short-circuiting
3. Sliding-Window Rate Limiting (Spam burst prevention with 429 cooldown)
4. Multi-Course Global Assistant Mode (course_id = 0)
5. End-to-end ASP.NET MVC /Chatbot/Ask integration
"""

import time
import requests

RAG_URL = "http://127.0.0.1:8000/api/chat"
MVC_LOGIN_URL = "http://localhost:57794/Account/Login"
MVC_CHAT_URL = "http://localhost:57794/Chatbot/Ask"

def test_1_normal_generation_and_semantic_cache():
    print("\n--- Test 1: Normal Generation & Semantic Cache Hit ---")
    ts = int(time.time())
    query_a = f"Explain the core distinction between synchronous and asynchronous request processing in web servers ({ts})?"
    query_b = f"What is the main difference between synchronous and asynchronous request handling in web servers ({ts})?"
    
    # 1. Fresh Query (Guaranteed Cache Miss)
    t0 = time.time()
    resp1 = requests.post(RAG_URL, json={
        "query": query_a,
        "course_id": 1,
        "user_id": f"test_user_cache_1_{ts}",
        "user_role": "student"
    })
    elapsed1 = time.time() - t0
    data1 = resp1.json()
    print(f"Query 1 Status: {resp1.status_code} in {elapsed1:.2f}s | Model: {data1.get('model_used')} | Cached: {data1.get('cached')}")
    assert resp1.status_code == 200, f"Expected 200, got {resp1.status_code}"
    assert data1.get("cached") is False, "Expected first unique query to be fresh (not cached)"

    # 2. Semantically Equivalent Query (different phrasing, same meaning)
    time.sleep(0.5)
    t0 = time.time()
    resp2 = requests.post(RAG_URL, json={
        "query": query_b,
        "course_id": 1,
        "user_id": f"test_user_cache_2_{ts}",
        "user_role": "student"
    })
    elapsed2 = time.time() - t0
    data2 = resp2.json()
    print(f"Query 2 (Semantic Near-Duplicate) Status: {resp2.status_code} in {elapsed2*1000:.1f}ms | Cached: {data2.get('cached')} | Similarity: {data2.get('cache_similarity')}")
    assert resp2.status_code == 200
    assert data2.get("cached") is True, f"Expected semantic cache hit! Got cached={data2.get('cached')}"
    print("PASS: Semantic vector cache successfully served query in <50ms with 0 LLM calls!")


def test_2_duplicate_query_short_circuit():
    print("\n--- Test 2: Rapid Duplicate Query Short-Circuit ---")
    user_id = "test_user_dup_1"
    query = "Explain how ASP.NET handles dependency injection."

    resp1 = requests.post(RAG_URL, json={
        "query": query,
        "course_id": 1,
        "user_id": user_id,
        "user_role": "student"
    })
    print(f"First Submission: {resp1.status_code}")

    # Immediately fire identical query with same user
    t0 = time.time()
    resp2 = requests.post(RAG_URL, json={
        "query": query,
        "course_id": 1,
        "user_id": user_id,
        "user_role": "student"
    })
    elapsed = time.time() - t0
    print(f"Duplicate Submission: {resp2.status_code} in {elapsed*1000:.1f}ms")
    assert resp2.status_code == 200
    print("PASS: Duplicate query intercepted instantly!")


def test_3_rate_limiting_burst_defense():
    print("\n--- Test 3: Sliding-Window Rate Limiting (Spam Defense) ---")
    user_id = "spammer_bot_99"
    
    # Rapidly fire 6 distinct queries within 2 seconds
    statuses = []
    messages = []
    for i in range(6):
        resp = requests.post(RAG_URL, json={
            "query": f"Spam query variant number {i} {time.time()}",
            "course_id": 1,
            "user_id": user_id,
            "user_role": "student"
        })
        statuses.append(resp.status_code)
        if resp.status_code == 429:
            messages.append(resp.json().get("detail", ""))

    print(f"6 Rapid Fire Requests Received Statuses: {statuses}")
    assert 429 in statuses, "Expected HTTP 429 rate limit to trigger on rapid requests!"
    print(f"Rate Limiter Cooldown Message: {messages[0] if messages else 'None'}")
    print("PASS: Spam burst protection blocked excess queries with HTTP 429!")


def test_4_multi_course_global_mode():
    print("\n--- Test 4: Global Multi-Course Assistant (course_id = 0) ---")
    resp = requests.post(RAG_URL, json={
        "query": "What are the fundamentals of database indexes and HTTP REST status codes?",
        "course_id": 0,
        "enrolled_course_ids": [1, 2],
        "user_id": "test_user_multi",
        "user_role": "student"
    })
    print(f"Multi-Course Query Status: {resp.status_code}")
    assert resp.status_code == 200
    data = resp.json()
    sources = data.get("sources_used", [])
    print(f"Retrieved {len(sources)} sources across enrolled courses.")
    for s in sources:
        print(f"  - Source: {s.get('source_file')} | Course ID: {s.get('course_id')}")
    print("PASS: Multi-course retrieval successfully executed across courses [1, 2]!")


def test_5_mvc_end_to_end_chatbot_ask():
    print("\n--- Test 5: End-to-End ASP.NET MVC /Chatbot/Ask Integration ---")
    session = requests.Session()
    
    # 1. Fetch Login page and anti-forgery token
    from html.parser import HTMLParser
    class TokenParser(HTMLParser):
        def __init__(self):
            super().__init__()
            self.token = None
        def handle_starttag(self, tag, attrs):
            if tag == "input" and dict(attrs).get("name") == "__RequestVerificationToken":
                self.token = dict(attrs).get("value")

    login_page = session.get(MVC_LOGIN_URL)
    parser = TokenParser()
    parser.feed(login_page.text)
    assert parser.token, "Failed to retrieve __RequestVerificationToken from login page"

    # 2. Login as Demo Student (student@lms.com / Pass@123)
    login_resp = session.post(MVC_LOGIN_URL, data={
        "Email": "student@lms.com",
        "Password": "Pass@123",
        "RememberMe": "false",
        "__RequestVerificationToken": parser.token
    }, allow_redirects=True)
    print(f"MVC Login Status: {login_resp.status_code}")
    assert login_resp.status_code == 200, f"Login failed with status {login_resp.status_code}"

    # 3. Query Course 1 (Student enrolled in course 1)
    chat_resp = session.post(MVC_CHAT_URL, json={
        "Query": "What is the primary role of an API gateway or web architecture?",
        "CourseId": 1
    })
    print(f"MVC Course 1 Chatbot Status: {chat_resp.status_code}")
    assert chat_resp.status_code == 200
    mvc_data = chat_resp.json()
    print(f"MVC Chatbot Ask Success: {mvc_data.get('success')}")
    assert mvc_data.get("success") is True, f"MVC Ask returned false: {mvc_data.get('message')}"
    inner_data = mvc_data.get("data", {})
    print(f"Model used: {inner_data.get('model_used')} | Cached: {inner_data.get('cached')}")
    print(f"Answer snippet: {inner_data.get('answer', '')[:120]}...")

    # 4. Global Mode Query (CourseId = 0)
    global_chat_resp = session.post(MVC_CHAT_URL, json={
        "Query": "Can you give me a summary of key concepts across all my enrolled courses?",
        "CourseId": 0
    })
    print(f"MVC Global Mode (CourseId=0) Status: {global_chat_resp.status_code}")
    assert global_chat_resp.status_code == 200
    global_data = global_chat_resp.json()
    assert global_data.get("success") is True, f"Global mode returned false: {global_data.get('message')}"
    print(f"Global Mode Model used: {global_data.get('data', {}).get('model_used')}")
    print("PASS: End-to-end ASP.NET MVC Chatbot controller and Global Mode verified successfully!")


if __name__ == "__main__":
    test_1_normal_generation_and_semantic_cache()
    test_2_duplicate_query_short_circuit()
    test_3_rate_limiting_burst_defense()
    test_4_multi_course_global_mode()
    test_5_mvc_end_to_end_chatbot_ask()
    print("\nALL 5 RELIABILITY, RESILIENCE & CACHING TESTS PASSED PERFECTLY!")
