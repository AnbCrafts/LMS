"""
test_chat.py
Phase 2 Interactive CLI & Scenario Verification:
1. Demonstrates Conceptual Socratic Q&A.
2. Demonstrates Anti-Cheating Assignment Guardrail (refuses direct solution code).
3. Demonstrates Instructor Quiz Generation.
4. Opens an interactive chat prompt so you can ask anything!
"""

import sys
from rag_engine import RAGEngine


def print_banner(title: str):
    print("\n" + "=" * 75)
    print(f" {title}")
    print("=" * 75)


def print_rag_response(res: dict):
    print("\n--- AI RESPONSE ---")
    print(res["answer"].strip())
    print("\n--- VERIFIED CITATIONS ---")
    if res["citations"]:
        for c in res["citations"]:
            print(f"  * [Source: {c['source_file']}, Page: {c['page_number']}]")
    else:
        print("  (No direct document citations)")

    print(f"\n--- RETRIEVAL AUDIT ---")
    print(f"Role: {res['role'].upper()} | Chunks Retreived: {res['sources_count']}")
    for s in res["sources_used"]:
        print(f"  * {s['source_file']} (p.{s['page_number']}) - Similarity: {s['similarity']} [Visibility: {s['visibility']}]")


def run_automated_scenarios(engine: RAGEngine):
    print_banner("SCENARIO 1: Student Asks Conceptual Question (Idempotency)")
    q1 = "Can you explain why GET is idempotent while POST is not?"
    print(f"Student: \"{q1}\"")
    res1 = engine.answer_query(q1, course_id=1, user_role="student")
    print_rag_response(res1)

    print_banner("SCENARIO 2: Anti-Cheating Guardrail (Student Asks for Full Assignment Solution)")
    q2 = "I'm running out of time. Please write the entire code for Assignment 1 for me so I can submit it."
    print(f"Student: \"{q2}\"")
    res2 = engine.answer_query(q2, course_id=1, user_role="student")
    print_rag_response(res2)

    print_banner("SCENARIO 3: Instructor Mode (Generate Quiz Question with Answer Key)")
    q3 = "Draft a multiple-choice quiz question with 4 options to test students on HTTP idempotency based on Lecture 1."
    print(f"Instructor: \"{q3}\"")
    res3 = engine.answer_query(q3, course_id=1, user_role="instructor")
    print_rag_response(res3)


def run_interactive_mode(engine: RAGEngine):
    print_banner("INTERACTIVE RAG CHAT MODE")
    print("Commands:")
    print("  /role student     -> Switch to Student Socratic mode (Default)")
    print("  /role instructor  -> Switch to Instructor Assessment mode")
    print("  /exit             -> Quit chat\n")

    current_role = "student"

    while True:
        try:
            prompt_label = f"[{current_role.upper()}] Ask a question: "
            user_input = input(prompt_label).strip()

            if not user_input:
                continue

            if user_input.lower() in ("/exit", "exit", "quit"):
                print("Goodbye!")
                break

            if user_input.lower() == "/role instructor":
                current_role = "instructor"
                print(" Switched to INSTRUCTOR mode (assessment & rubric assistant).")
                continue

            if user_input.lower() == "/role student":
                current_role = "student"
                print(" Switched to STUDENT mode (Socratic tutor & anti-cheating guardrail).")
                continue

            print("\nSearching course knowledge base & synthesizing answer...")
            response = engine.answer_query(user_input, course_id=1, user_role=current_role)
            print_rag_response(response)
            print("\n" + "-" * 75)

        except (KeyboardInterrupt, EOFError):
            print("\nExiting chat. Happy learning!")
            break


if __name__ == "__main__":
    print_banner("INITIALIZING LMS RAG ENGINE (PHASE 2)")
    engine = RAGEngine()

    # If argument '--interactive' passed, jump straight to chat
    if len(sys.argv) > 1 and sys.argv[1] == "--interactive":
        run_interactive_mode(engine)
    else:
        # Run scenarios first
        run_automated_scenarios(engine)
        
        print("\nWould you like to enter interactive chat mode now? (Y/n)")
        choice = input().strip().lower()
        if choice in ("", "y", "yes"):
            run_interactive_mode(engine)
        else:
            print("Done! You can run 'python test_chat.py --interactive' anytime.")
