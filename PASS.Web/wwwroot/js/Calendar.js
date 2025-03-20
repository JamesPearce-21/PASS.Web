// Ensure Calendar is defined globally
window.Calendar = class Calendar extends React.Component {
    constructor(props) {
        super(props);
        this.state = {
            events: [
                { date: "2024-06-10", title: "Sports Day" },
                { date: "2024-06-15", title: "Football Tournament" },
                { date: "2024-07-01", title: "Summer Camp Registration" }
            ]
        };
    }

    render() {
        return (
            <div className="calendar-wrapper">
                <h2>Events Calendar</h2>
                <div className="calendar-grid">
                    {this.state.events.map((event, index) => (
                        <div key={index} className="calendar-event">
                            <strong>{event.date}</strong>
                            <p>{event.title}</p>
                        </div>
                    ))}
                </div>
            </div>
        );
    }
};
