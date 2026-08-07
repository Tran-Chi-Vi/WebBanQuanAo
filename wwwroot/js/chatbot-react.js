/* ==========================================================================
   WEBBANQUANAO - React Powered AI Chatbot Widget Component
   ========================================================================== */

const { useState, useEffect, useRef } = React;

function ChatbotWidget() {
  const [isOpen, setIsOpen] = useState(false);
  const [messages, setMessages] = useState([
    {
      sender: 'bot',
      text: 'Xin chào! Tôi là Trợ Lý Thời Trang AI. Tôi có thể giúp bạn tìm mẫu quần áo, gợi ý chọn size hoặc tra cứu đơn hàng!',
      data: null
    }
  ]);
  const [inputMsg, setInputMsg] = useState('');
  const [loading, setLoading] = useState(false);
  const chatEndRef = useRef(null);

  const scrollToBottom = () => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    if (isOpen) scrollToBottom();
  }, [messages, isOpen]);

  const handleSend = async (e) => {
    e?.preventDefault();
    if (!inputMsg.trim() || loading) return;

    const userText = inputMsg.trim();
    setInputMsg('');
    setMessages(prev => [...prev, { sender: 'user', text: userText }]);
    setLoading(true);

    try {
      const res = await fetch('/Chatbot/SendMessage', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: userText })
      });
      const data = await res.json();
      setMessages(prev => [...prev, { sender: 'bot', text: data.reply, data: data.data }]);
    } catch (err) {
      setMessages(prev => [...prev, { sender: 'bot', text: 'Rất tiếc, đã có lỗi kết nối. Vui lòng thử lại sau.' }]);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ position: 'fixed', bottom: '20px', right: '20px', zIndex: 99999 }}>
      {/* Floating Toggle Button - Compact Icon Circle */}
      {!isOpen && (
        <button
          onClick={() => setIsOpen(true)}
          className="btn btn-primary-gradient shadow-lg d-flex align-items-center justify-content-center"
          style={{ width: '46px', height: '46px', borderRadius: '50%', padding: '0', fontSize: '1.2rem' }}
          title="Trợ Lý AI Trực Tuyến"
        >
          <i className="bi bi-chat-dots-fill"></i>
        </button>
      )}

      {/* Chat Window Glassmorphism - Compact Dimensions */}
      {isOpen && (
        <div
          className="card shadow-2xl border-0 animate__animated animate__fadeInUp"
          style={{
            width: '320px',
            height: '430px',
            borderRadius: '18px',
            overflow: 'hidden',
            background: 'rgba(255, 255, 255, 0.96)',
            backdropFilter: 'blur(20px)',
            border: '1px solid rgba(226, 232, 240, 0.8)'
          }}
        >
          {/* Chat Header */}
          <div
            className="p-3 text-white d-flex align-items-center justify-content-between"
            style={{ background: 'linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%)' }}
          >
            <div className="d-flex align-items-center gap-2">
              <div className="bg-white text-primary rounded-circle p-2 d-flex align-items-center justify-content-center" style={{ width: 36, height: 36 }}>
                <i className="bi bi-robot fs-5"></i>
              </div>
              <div>
                <h6 className="m-0 font-heading fw-bold">Fashion AI Assistant</h6>
                <small className="opacity-75" style={{ fontSize: '0.75rem' }}>● Sẵn sàng tư vấn</small>
              </div>
            </div>
            <button onClick={() => setIsOpen(false)} className="btn-close btn-close-white"></button>
          </div>

          {/* Chat Body */}
          <div className="p-3 flex-grow-1 overflow-auto" style={{ height: '380px', background: '#f8fafc' }}>
            {messages.map((m, idx) => (
              <div key={idx} className={`d-flex mb-3 ${m.sender === 'user' ? 'justify-content-end' : 'justify-content-start'}`}>
                <div
                  className={`p-3 rounded-4 max-w-75 text-sm shadow-sm ${
                    m.sender === 'user'
                      ? 'bg-primary text-white rounded-bottom-right-0'
                      : 'bg-white text-dark border rounded-bottom-left-0'
                  }`}
                  style={{ maxWidth: '82%', fontSize: '0.9rem' }}
                >
                  <p className="m-0" style={{ whiteSpace: 'pre-line' }}>{m.text}</p>
                  
                  {/* Render Product Suggestion Cards if available */}
                  {m.data && Array.isArray(m.data) && (
                    <div className="mt-2 d-flex flex-column gap-2">
                      {m.data.map(p => (
                        <a
                          key={p.id}
                          href={p.url}
                          className="d-flex align-items-center gap-2 p-2 bg-light rounded text-decoration-none text-dark border hover-shadow"
                        >
                          <img src={p.image} alt={p.name} style={{ width: 42, height: 42, objectFit: 'cover', borderRadius: 8 }} />
                          <div style={{ flex: 1, minWidth: 0 }}>
                            <div className="fw-bold text-truncate" style={{ fontSize: '0.82rem' }}>{p.name}</div>
                            <div className="text-primary fw-bold" style={{ fontSize: '0.8rem' }}>{p.price}</div>
                          </div>
                          <i className="bi bi-chevron-right text-muted"></i>
                        </a>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            ))}
            {loading && (
              <div className="d-flex align-items-center gap-2 text-muted small ms-2 mb-2">
                <div className="spinner-grow spinner-grow-sm text-primary" role="status"></div>
                <span>Đang suy nghĩ...</span>
              </div>
            )}
            <div ref={chatEndRef} />
          </div>

          {/* Chat Input Footer */}
          <form onSubmit={handleSend} className="p-2 border-top bg-white d-flex gap-2">
            <input
              type="text"
              className="form-control border-0 bg-light rounded-pill px-3"
              placeholder="Nhập câu hỏi hoặc từ khóa..."
              value={inputMsg}
              onChange={e => setInputMsg(e.target.value)}
            />
            <button type="submit" className="btn btn-primary-gradient rounded-circle p-2 d-flex align-items-center justify-content-center" style={{ width: 40, height: 40 }}>
              <i className="bi bi-send-fill"></i>
            </button>
          </form>
        </div>
      )}
    </div>
  );
}

// Render React Chatbot Widget into container
const rootElem = document.getElementById('react-chatbot-root');
if (rootElem) {
  const root = ReactDOM.createRoot(rootElem);
  root.render(<ChatbotWidget />);
}
