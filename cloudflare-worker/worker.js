/**
 * Cloudflare Worker entry point
 * Simple Hello World handler for initial setup verification
 */
export default {
  async fetch(request, env, ctx) {
    // Return a simple JSON response
    const response = {
      message: "Hello World",
      status: "ok"
    };

    return new Response(JSON.stringify(response), {
      status: 200,
      headers: {
        "Content-Type": "application/json"
      }
    });
  }
};
